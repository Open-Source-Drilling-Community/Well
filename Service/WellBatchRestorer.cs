using Microsoft.Data.Sqlite;
using OSDC.DotnetLibraries.General.DataManagement;
using OSDC.Drilling.Well.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using WellModel = OSDC.Drilling.Well.Model.Well;

namespace OSDC.Drilling.Well.Service;

public enum WellBatchRestoreFailureKind { None, InvalidRequest, Conflict, StorageFailure }

public sealed class WellBatchRestoreOutcome
{
    public WellBatchRestoreResponse? Response { get; init; }
    public WellBatchErrorEnvelope? Error { get; init; }
    public WellBatchRestoreFailureKind FailureKind { get; init; }
    public bool IsSuccess => Response != null && FailureKind == WellBatchRestoreFailureKind.None;
}

/// <summary>Validates, maps catalogs, and restores the complete batch in one transaction.</summary>
public static class WellBatchRestorer
{
    public static WellBatchRestoreOutcome Restore(SqliteConnection connection,
        WellBatchRestoreRequest? request, DateTimeOffset restoredAtUtc)
    {
        List<WellBatchError> validationErrors = ValidateRequest(request);
        if (validationErrors.Count != 0) return Failure(WellBatchRestoreFailureKind.InvalidRequest,
            "invalid_batch_restore_request", "The Well batch-restore request is invalid. No changes were made.", validationErrors);

        using SqliteTransaction transaction = connection.BeginTransaction();
        try
        {
            CatalogState catalogs = CatalogState.Load(connection, transaction);
            List<WellModel> wells = CloneWells(request!.Document!.Wells);
            List<WellBatchCatalogMapping> mappings = [];
            List<WellBatchError> mappingErrors = [];
            int createdDefinitions = 0;
            int createdOptions = 0;
            bool createMissing = request.CatalogPolicy == WellBatchCatalogRestorePolicy.MapOrCreateMissing;

            ResolveDependencies(request.Document.CatalogDependencies, catalogs, createMissing, mappings,
                mappingErrors, restoredAtUtc, ref createdDefinitions, ref createdOptions);
            if (mappingErrors.Count != 0)
            {
                transaction.Rollback();
                return Failure(WellBatchRestoreFailureKind.Conflict, "catalog_restore_conflict",
                    "Catalog references could not be resolved unambiguously. No changes were made.", mappingErrors);
            }
            RewriteReferences(wells, mappings);

            List<PreparedWell> prepared = PrepareWells(wells);
            List<bool> exists = prepared.Select(value => RowExists(connection, transaction, value.ID)).ToList();
            if (request.ConflictPolicy == WellBatchRestoreConflictPolicy.FailIfExists)
            {
                List<WellBatchError> conflicts = prepared.Select((value, index) => (value, index))
                    .Where(value => exists[value.index])
                    .Select(value => Error(value.index, "Document.Wells", "well_already_exists",
                        $"A stored Well already has UUID '{value.value.ID}'."))
                    .ToList();
                if (conflicts.Count != 0)
                {
                    transaction.Rollback();
                    return Failure(WellBatchRestoreFailureKind.Conflict, "well_restore_conflict",
                        "One or more Well UUIDs already exist. No changes were made.", conflicts);
                }
            }

            catalogs.Save(connection, transaction);
            SaveWells(connection, transaction, prepared, request.ConflictPolicy);
            transaction.Commit();
            return new WellBatchRestoreOutcome
            {
                Response = new WellBatchRestoreResponse
                {
                    RestoredAtUtc = restoredAtUtc.ToUniversalTime(),
                    CreatedCount = exists.Count(value => !value),
                    ReplacedCount = exists.Count(value => value),
                    CreatedCatalogDefinitionCount = createdDefinitions,
                    CreatedCatalogOptionCount = createdOptions,
                    CatalogMappings = mappings,
                    WellIDs = prepared.Select(value => value.ID).ToList()
                }
            };
        }
        catch (Exception exception) when (exception is SqliteException or JsonException or InvalidOperationException or KeyNotFoundException)
        {
            try { transaction.Rollback(); } catch (InvalidOperationException) { }
            return StorageFailure($"The Well database rejected the batch. No changes were committed. {exception.Message}");
        }
    }

    public static WellBatchRestoreOutcome StorageFailure(string message) => Failure(
        WellBatchRestoreFailureKind.StorageFailure, "well_restore_failed", message,
        [Error(null, "Document.Wells", "storage_failure", "The complete restore transaction was rolled back.")]);

    public static List<WellBatchError> ValidateRequest(WellBatchRestoreRequest? request)
    {
        if (request == null) return [Error(null, "Request", "required", "A batch-restore request is required.")];
        List<WellBatchError> errors = [];
        if (request.ConflictPolicy is not WellBatchRestoreConflictPolicy.FailIfExists and not WellBatchRestoreConflictPolicy.ReplaceExisting)
            errors.Add(Error(null, "ConflictPolicy", "invalid_conflict_policy", "ConflictPolicy must be FailIfExists or ReplaceExisting."));
        if (request.CatalogPolicy is not WellBatchCatalogRestorePolicy.MapExisting and not WellBatchCatalogRestorePolicy.MapOrCreateMissing)
            errors.Add(Error(null, "CatalogPolicy", "invalid_catalog_policy", "CatalogPolicy must be MapExisting or MapOrCreateMissing."));
        WellBatchExportDocument? document = request.Document;
        if (document == null)
        {
            errors.Add(Error(null, "Document", "required", "A batch-export document is required."));
            return errors;
        }
        if (document.FormatIdentifier != WellBatchExportDocument.CurrentFormatIdentifier)
            errors.Add(Error(null, "Document.FormatIdentifier", "unsupported_format", $"FormatIdentifier must be '{WellBatchExportDocument.CurrentFormatIdentifier}'."));
        if (document.SchemaVersion != WellBatchExportDocument.CurrentSchemaVersion)
            errors.Add(Error(null, "Document.SchemaVersion", "unsupported_schema_version", $"SchemaVersion must be {WellBatchExportDocument.CurrentSchemaVersion}."));
        if (document.ExportedAtUtc == default || document.ExportedAtUtc.Offset != TimeSpan.Zero)
            errors.Add(Error(null, "Document.ExportedAtUtc", "invalid_export_timestamp", "ExportedAtUtc must be a non-default UTC timestamp."));
        ValidateDependencies(document.CatalogDependencies, errors);
        if (document.Wells == null || document.Wells.Count == 0)
        {
            errors.Add(Error(null, "Document.Wells", "required", "At least one Well is required for restore."));
            return errors;
        }
        ValidateReferences(document.Wells, document.CatalogDependencies, errors);
        Dictionary<Guid, int> positions = [];
        for (int index = 0; index < document.Wells.Count; index++)
        {
            WellModel? well = document.Wells[index];
            Guid? id = well?.MetaInfo?.ID;
            if (well == null) errors.Add(Error(index, "Document.Wells", "null_well", "A restored Well must not be null."));
            else if (id == null || id == Guid.Empty) errors.Add(Error(index, "Document.Wells.MetaInfo.ID", "empty_uuid", "Every restored Well must have a non-empty UUID."));
            else if (positions.TryGetValue(id.Value, out int first)) errors.Add(Error(index, "Document.Wells.MetaInfo.ID", "duplicate_uuid", $"Well UUID '{id}' duplicates position {first}."));
            else positions.Add(id.Value, index);
            if (well?.ClusterID == Guid.Empty) errors.Add(Error(index, "Document.Wells.ClusterID", "empty_uuid", "ClusterID must be omitted or a non-empty UUID."));
            if (well?.SlotID == Guid.Empty) errors.Add(Error(index, "Document.Wells.SlotID", "empty_uuid", "SlotID must be omitted or a non-empty UUID."));
        }
        return errors;
    }

    private static void ValidateDependencies(WellBatchCatalogDependencies? dependencies, List<WellBatchError> errors)
    {
        if (dependencies == null)
        {
            errors.Add(Error(null, "Document.CatalogDependencies", "required", "CatalogDependencies is required."));
            return;
        }
        HashSet<Guid> ids = [];
        void Check(Guid id, string? name, string property)
        {
            if (id == Guid.Empty) errors.Add(Error(null, property, "empty_uuid", "Catalog UUIDs must be non-empty."));
            else if (!ids.Add(id)) errors.Add(Error(null, property, "duplicate_uuid", $"Catalog UUID '{id}' occurs more than once."));
            if (string.IsNullOrWhiteSpace(name)) errors.Add(Error(null, property + ".Name", "required", "Catalog names must not be empty."));
        }
        foreach (WellIdentity? identity in dependencies.Identities ?? [])
            Check(identity?.MetaInfo?.ID ?? Guid.Empty, identity?.Name, "Document.CatalogDependencies.Identities");
        foreach (WellFeatureCategory? category in dependencies.FeatureCategories ?? [])
        {
            Check(category?.MetaInfo?.ID ?? Guid.Empty, category?.Name, "Document.CatalogDependencies.FeatureCategories");
            foreach (WellFeatureOption option in category?.Options ?? [])
                Check(option.ID, option.Name, "Document.CatalogDependencies.FeatureCategories.Options");
        }
    }

    private static void ValidateReferences(List<WellModel> wells, WellBatchCatalogDependencies? dependencies,
        List<WellBatchError> errors)
    {
        if (dependencies == null) return;
        HashSet<Guid> identityIds = (dependencies.Identities ?? [])
            .Where(value => value?.MetaInfo?.ID is Guid id && id != Guid.Empty)
            .Select(value => value.MetaInfo!.ID).ToHashSet();
        Dictionary<Guid, HashSet<Guid>> categoryOptions = [];
        foreach (WellFeatureCategory? category in dependencies.FeatureCategories ?? [])
        {
            if (category?.MetaInfo?.ID is not Guid categoryId || categoryId == Guid.Empty || categoryOptions.ContainsKey(categoryId))
                continue;
            categoryOptions.Add(categoryId, (category.Options ?? []).Where(option => option != null).Select(option => option.ID).ToHashSet());
        }
        for (int index = 0; index < wells.Count; index++)
        {
            foreach (WellIdentityAssignment? assignment in wells[index]?.WellIdentityAssignments ?? [])
            {
                if (assignment?.IdentityID is not Guid id || id == Guid.Empty || !identityIds.Contains(id))
                    errors.Add(Error(index, "Document.Wells.WellIdentityAssignments.IdentityID", "catalog_dependency_missing", $"Referenced identity '{assignment?.IdentityID}' is absent from CatalogDependencies."));
            }
            foreach (WellFeatureAssignment? assignment in wells[index]?.WellFeatureAssignments ?? [])
            {
                if (assignment?.FeatureCategoryID is not Guid categoryId || !categoryOptions.TryGetValue(categoryId, out HashSet<Guid>? options))
                    errors.Add(Error(index, "Document.Wells.WellFeatureAssignments.FeatureCategoryID", "catalog_dependency_missing", $"Referenced category '{assignment?.FeatureCategoryID}' is absent from CatalogDependencies."));
                else if (assignment.FeatureOptionID is not Guid optionId || !options.Contains(optionId))
                    errors.Add(Error(index, "Document.Wells.WellFeatureAssignments.FeatureOptionID", "catalog_dependency_missing", $"Referenced option '{assignment.FeatureOptionID}' is absent from category '{categoryId}'."));
            }
        }
    }

    private static void ResolveDependencies(WellBatchCatalogDependencies dependencies, CatalogState local,
        bool createMissing, List<WellBatchCatalogMapping> mappings, List<WellBatchError> errors,
        DateTimeOffset now, ref int createdDefinitions, ref int createdOptions)
    {
        foreach (WellIdentity source in dependencies.Identities ?? [])
        {
            Guid sourceId = source.MetaInfo!.ID;
            WellIdentity? target = ResolveFlat(sourceId, source.Name, local.Identities, createMissing, errors);
            bool created = false;
            if (target == null && createMissing && !HasErrorFor(errors, sourceId))
            {
                target = new WellIdentity { MetaInfo = new MetaInfo { ID = Guid.NewGuid() }, Name = source.Name,
                    CreationDate = now, LastModificationDate = now };
                local.Identities.Add(target); local.DirtyIdentities.Add(target); createdDefinitions++; created = true;
            }
            if (target != null) AddMapping(mappings, "Identity", source.Name, sourceId, target.MetaInfo!.ID,
                sourceId == target.MetaInfo.ID ? "exact_uuid" : created ? "created" : "normalized_name");
        }
        foreach (WellFeatureCategory source in dependencies.FeatureCategories ?? [])
            ResolveCategory(source, local, createMissing, mappings, errors, now, ref createdDefinitions, ref createdOptions);
    }

    private static void ResolveCategory(WellFeatureCategory source, CatalogState local, bool createMissing,
        List<WellBatchCatalogMapping> mappings, List<WellBatchError> errors, DateTimeOffset now,
        ref int createdDefinitions, ref int createdOptions)
    {
        Guid sourceId = source.MetaInfo!.ID;
        WellFeatureCategory? target = local.Features.FirstOrDefault(value => value.MetaInfo!.ID == sourceId);
        bool created = false;
        if (target != null && (!SameName(target.Name, source.Name) || target.IsExclusive != source.IsExclusive || target.HasValidityPeriod != source.HasValidityPeriod))
        {
            AddSemanticConflict(errors, "feature category", sourceId, source.Name); return;
        }
        if (target == null)
        {
            List<WellFeatureCategory> matches = local.Features.Where(value => SameName(value.Name, source.Name)).ToList();
            if (matches.Count > 1) { AddAmbiguous(errors, "feature category", sourceId, source.Name); return; }
            if (matches.Count == 1)
            {
                target = matches[0];
                if (target.IsExclusive != source.IsExclusive || target.HasValidityPeriod != source.HasValidityPeriod)
                { AddSemanticConflict(errors, "feature category", sourceId, source.Name); return; }
            }
            else if (createMissing)
            {
                target = new WellFeatureCategory { MetaInfo = new MetaInfo { ID = Guid.NewGuid() }, Name = source.Name,
                    IsExclusive = source.IsExclusive, HasValidityPeriod = source.HasValidityPeriod, Options = [],
                    CreationDate = now, LastModificationDate = now };
                local.Features.Add(target); local.DirtyFeatures.Add(target); createdDefinitions++; created = true;
            }
            else { AddMissing(errors, "feature category", sourceId, source.Name); return; }
        }
        AddMapping(mappings, "FeatureCategory", source.Name, sourceId, target.MetaInfo!.ID,
            sourceId == target.MetaInfo.ID ? "exact_uuid" : created ? "created" : "normalized_name");
        foreach (WellFeatureOption sourceOption in source.Options ?? [])
        {
            WellFeatureOption? targetOption = (target.Options ?? []).FirstOrDefault(value => value.ID == sourceOption.ID);
            bool optionCreated = false;
            if (targetOption != null && !SameName(targetOption.Name, sourceOption.Name))
            { AddSemanticConflict(errors, "feature option", sourceOption.ID, sourceOption.Name); continue; }
            if (targetOption == null)
            {
                List<WellFeatureOption> matches = (target.Options ?? []).Where(value => SameName(value.Name, sourceOption.Name)).ToList();
                if (matches.Count > 1) { AddAmbiguous(errors, "feature option", sourceOption.ID, sourceOption.Name); continue; }
                if (matches.Count == 1) targetOption = matches[0];
                else if (createMissing)
                {
                    targetOption = new WellFeatureOption { ID = Guid.NewGuid(), Name = sourceOption.Name };
                    target.Options ??= []; target.Options.Add(targetOption); target.LastModificationDate = now;
                    local.DirtyFeatures.Add(target); createdOptions++; optionCreated = true;
                }
                else { AddMissing(errors, "feature option", sourceOption.ID, sourceOption.Name); continue; }
            }
            AddMapping(mappings, "FeatureOption", sourceOption.Name, sourceOption.ID, targetOption.ID,
                sourceOption.ID == targetOption.ID ? "exact_uuid" : optionCreated ? "created" : "normalized_name");
        }
    }

    private static WellIdentity? ResolveFlat(Guid sourceId, string? sourceName, List<WellIdentity> local,
        bool createMissing, List<WellBatchError> errors)
    {
        WellIdentity? exact = local.FirstOrDefault(value => value.MetaInfo!.ID == sourceId);
        if (exact != null)
        {
            if (!SameName(exact.Name, sourceName)) AddSemanticConflict(errors, "identity", sourceId, sourceName);
            return HasErrorFor(errors, sourceId) ? null : exact;
        }
        List<WellIdentity> matches = local.Where(value => SameName(value.Name, sourceName)).ToList();
        if (matches.Count == 1) return matches[0];
        if (matches.Count > 1) AddAmbiguous(errors, "identity", sourceId, sourceName);
        else if (!createMissing) AddMissing(errors, "identity", sourceId, sourceName);
        return null;
    }

    private static void RewriteReferences(List<WellModel> wells, List<WellBatchCatalogMapping> mappings)
    {
        Dictionary<Guid, Guid> map = mappings.ToDictionary(value => value.SourceID, value => value.LocalID);
        foreach (WellModel well in wells)
        {
            foreach (WellIdentityAssignment assignment in well.WellIdentityAssignments ?? [])
                if (assignment.IdentityID is Guid id) assignment.IdentityID = map[id];
            foreach (WellFeatureAssignment assignment in well.WellFeatureAssignments ?? [])
            {
                if (assignment.FeatureCategoryID is Guid categoryId) assignment.FeatureCategoryID = map[categoryId];
                if (assignment.FeatureOptionID is Guid optionId) assignment.FeatureOptionID = map[optionId];
            }
        }
    }

    private static List<WellModel> CloneWells(List<WellModel> values) => JsonSerializer.Deserialize<List<WellModel>>(
        JsonSerializer.Serialize(values, JsonSettings.Options), JsonSettings.Options) ?? throw new JsonException("Wells could not be cloned.");
    private static List<PreparedWell> PrepareWells(List<WellModel> values) => values.Select(value => new PreparedWell(
        value.MetaInfo!.ID, JsonSerializer.Serialize(value.MetaInfo, JsonSettings.Options),
        value.ClusterID?.ToString() ?? "", value.SlotID?.ToString() ?? "",
        JsonSerializer.Serialize(value, JsonSettings.Options))).ToList();
    private static bool RowExists(SqliteConnection connection, SqliteTransaction transaction, Guid id)
    { using SqliteCommand command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "SELECT COUNT(*) FROM WellTable WHERE ID=$id"; command.Parameters.AddWithValue("$id", id.ToString()); return Convert.ToInt64(command.ExecuteScalar()) != 0; }
    private static void SaveWells(SqliteConnection connection, SqliteTransaction transaction,
        List<PreparedWell> wells, WellBatchRestoreConflictPolicy policy)
    {
        foreach (PreparedWell well in wells)
        {
            using SqliteCommand command = connection.CreateCommand(); command.Transaction = transaction;
            command.CommandText = policy == WellBatchRestoreConflictPolicy.ReplaceExisting
                ? "INSERT INTO WellTable (ID,MetaInfo,ClusterID,SlotID,Well) VALUES ($id,$meta,$cluster,$slot,$doc) ON CONFLICT(ID) DO UPDATE SET MetaInfo=excluded.MetaInfo,ClusterID=excluded.ClusterID,SlotID=excluded.SlotID,Well=excluded.Well"
                : "INSERT INTO WellTable (ID,MetaInfo,ClusterID,SlotID,Well) VALUES ($id,$meta,$cluster,$slot,$doc)";
            command.Parameters.AddWithValue("$id", well.ID.ToString());
            command.Parameters.AddWithValue("$meta", well.MetaInfoJson);
            command.Parameters.AddWithValue("$cluster", well.ClusterID);
            command.Parameters.AddWithValue("$slot", well.SlotID);
            command.Parameters.AddWithValue("$doc", well.WellJson);
            command.ExecuteNonQuery();
        }
    }

    private static string Normalize(string? value) => string.Join(' ', (value ?? "").Normalize(NormalizationForm.FormKC)
        .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
    private static bool SameName(string? left, string? right) => Normalize(left) == Normalize(right);
    private static bool HasErrorFor(List<WellBatchError> errors, Guid id) => errors.Any(error => error.Message.Contains(id.ToString(), StringComparison.OrdinalIgnoreCase));
    private static void AddMissing(List<WellBatchError> errors, string kind, Guid id, string? name) => errors.Add(Error(null, $"Document.CatalogDependencies[{id}]", "catalog_definition_missing", $"No compatible local {kind} exists for '{name}' ({id}), and creation is disabled."));
    private static void AddAmbiguous(List<WellBatchError> errors, string kind, Guid id, string? name) => errors.Add(Error(null, $"Document.CatalogDependencies[{id}]", "ambiguous_catalog_match", $"More than one local {kind} has normalized name '{name}' for source UUID '{id}'."));
    private static void AddSemanticConflict(List<WellBatchError> errors, string kind, Guid id, string? name) => errors.Add(Error(null, $"Document.CatalogDependencies[{id}]", "catalog_semantic_conflict", $"The local {kind} corresponding to '{name}' ({id}) has incompatible semantics."));
    private static void AddMapping(List<WellBatchCatalogMapping> mappings, string catalog, string? name, Guid source, Guid local, string resolution) => mappings.Add(new() { Catalog = catalog, Name = name ?? "", SourceID = source, LocalID = local, Resolution = resolution });
    private static WellBatchRestoreOutcome Failure(WellBatchRestoreFailureKind kind, string error, string message, List<WellBatchError> errors) => new() { FailureKind = kind, Error = new() { Error = error, Message = message, Errors = errors } };
    private static WellBatchError Error(int? index, string property, string code, string message) => new() { PositionIndex = index, Property = property, Code = code, Message = message };
    private sealed record PreparedWell(Guid ID, string MetaInfoJson, string ClusterID, string SlotID, string WellJson);

    private sealed class CatalogState
    {
        public List<WellIdentity> Identities { get; } = [];
        public List<WellFeatureCategory> Features { get; } = [];
        public HashSet<WellIdentity> DirtyIdentities { get; } = [];
        public HashSet<WellFeatureCategory> DirtyFeatures { get; } = [];

        public static CatalogState Load(SqliteConnection connection, SqliteTransaction transaction)
        {
            CatalogState state = new();
            state.Identities.AddRange(Read<WellIdentity>(connection, transaction, "WellIdentityTable", "WellIdentity"));
            state.Features.AddRange(Read<WellFeatureCategory>(connection, transaction, "WellFeatureCategoryTable", "WellFeatureCategory"));
            return state;
        }
        private static List<T> Read<T>(SqliteConnection connection, SqliteTransaction transaction, string table, string column)
        {
            using SqliteCommand command = connection.CreateCommand(); command.Transaction = transaction;
            command.CommandText = $"SELECT {column} FROM {table}";
            using SqliteDataReader reader = command.ExecuteReader(); List<T> result = [];
            while (reader.Read()) result.Add(JsonSerializer.Deserialize<T>(reader.GetString(0), JsonSettings.Options) ?? throw new JsonException($"Invalid {table} document."));
            return result;
        }
        public void Save(SqliteConnection connection, SqliteTransaction transaction)
        {
            foreach (WellIdentity value in DirtyIdentities)
            {
                using SqliteCommand command = connection.CreateCommand(); command.Transaction = transaction;
                command.CommandText = "INSERT INTO WellIdentityTable (ID,MetaInfo,Name,CreationDate,LastModificationDate,WellIdentity) VALUES ($id,$meta,$name,$created,$modified,$doc)";
                AddCommon(command, value.MetaInfo!, value.Name, value.CreationDate, value.LastModificationDate, value); command.ExecuteNonQuery();
            }
            foreach (WellFeatureCategory value in DirtyFeatures)
            {
                using SqliteCommand command = connection.CreateCommand(); command.Transaction = transaction;
                command.CommandText = "INSERT INTO WellFeatureCategoryTable (ID,MetaInfo,Name,IsExclusive,HasValidityPeriod,CreationDate,LastModificationDate,WellFeatureCategory) VALUES ($id,$meta,$name,$exclusive,$validity,$created,$modified,$doc) ON CONFLICT(ID) DO UPDATE SET MetaInfo=excluded.MetaInfo,Name=excluded.Name,IsExclusive=excluded.IsExclusive,HasValidityPeriod=excluded.HasValidityPeriod,CreationDate=excluded.CreationDate,LastModificationDate=excluded.LastModificationDate,WellFeatureCategory=excluded.WellFeatureCategory";
                AddCommon(command, value.MetaInfo!, value.Name, value.CreationDate, value.LastModificationDate, value);
                command.Parameters.AddWithValue("$exclusive", value.IsExclusive ? 1 : 0);
                command.Parameters.AddWithValue("$validity", value.HasValidityPeriod ? 1 : 0); command.ExecuteNonQuery();
            }
        }
        private static void AddCommon(SqliteCommand command, MetaInfo metaInfo, string? name,
            DateTimeOffset? created, DateTimeOffset? modified, object document)
        {
            command.Parameters.AddWithValue("$id", metaInfo.ID.ToString());
            command.Parameters.AddWithValue("$meta", JsonSerializer.Serialize(metaInfo, JsonSettings.Options));
            command.Parameters.AddWithValue("$name", name ?? "");
            command.Parameters.AddWithValue("$created", created?.ToString(Managers.SqlConnectionManager.DATE_TIME_FORMAT) ?? "");
            command.Parameters.AddWithValue("$modified", modified?.ToString(Managers.SqlConnectionManager.DATE_TIME_FORMAT) ?? "");
            command.Parameters.AddWithValue("$doc", JsonSerializer.Serialize(document, JsonSettings.Options));
        }
    }
}
