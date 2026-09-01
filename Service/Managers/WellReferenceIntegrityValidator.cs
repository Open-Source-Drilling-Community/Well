using Microsoft.Data.Sqlite;
using OSDC.Drilling.Well.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace OSDC.Drilling.Well.Service.Managers;

internal static class WellReferenceIntegrityValidator
{
    private sealed record CategoryDefinition(bool IsExclusive, bool HasValidityPeriod, HashSet<Guid> Options);

    public static List<WellMutationError> ValidateWell(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Model.Well well)
    {
        Dictionary<Guid, CategoryDefinition> categories = ReadCategoryDefinitions(connection, transaction);
        HashSet<Guid> identities = ReadDefinitionIds<WellIdentity>(connection, transaction, "WellIdentityTable", "WellIdentity", value => value.MetaInfo?.ID);

        List<WellMutationError> errors = [];
        if (well.ClusterID == Guid.Empty)
            errors.Add(Error("ClusterID", "empty_uuid", "ClusterID must be null or a non-empty UUID."));
        if (well.SlotID == Guid.Empty)
            errors.Add(Error("SlotID", "empty_uuid", "SlotID must be null or a non-empty UUID."));
        if (well.SlotID is not null && well.ClusterID is null)
            errors.Add(Error("SlotID", "cluster_required", "A Well assigned to a slot must also be assigned to a cluster."));

        HashSet<Guid> assignmentIds = [];
        for (int index = 0; index < (well.WellFeatureAssignments?.Count ?? 0); index++)
        {
            WellFeatureAssignment? assignment = well.WellFeatureAssignments![index];
            string path = $"WellFeatureAssignments[{index}]";
            if (assignment is null)
            {
                errors.Add(Error(path, "null_assignment", "Assignments cannot be null."));
                continue;
            }
            ValidateAssignmentId(assignment.ID, assignmentIds, $"{path}.ID", errors);
            ValidateFeatureAssignment(assignment, categories, path, errors);
        }
        for (int index = 0; index < (well.WellIdentityAssignments?.Count ?? 0); index++)
        {
            WellIdentityAssignment? assignment = well.WellIdentityAssignments![index];
            string path = $"WellIdentityAssignments[{index}]";
            if (assignment is null)
            {
                errors.Add(Error(path, "null_assignment", "Assignments cannot be null."));
                continue;
            }
            ValidateAssignmentId(assignment.ID, assignmentIds, $"{path}.ID", errors);
            ValidateRequiredReference(assignment.IdentityID, identities, $"{path}.IdentityID", "well_identity_not_found", errors);
            if (string.IsNullOrWhiteSpace(assignment.Value))
                errors.Add(Error($"{path}.Value", "value_required", "An identity assignment requires a non-blank value."));
        }

        ValidateExclusiveCategoryPeriods(well.WellFeatureAssignments ?? [], categories, errors);
        return errors;
    }

    public static WellMutationError? FindFeatureCategoryReferences(SqliteConnection connection, SqliteTransaction transaction,
        Guid categoryId, IReadOnlyCollection<Guid>? permittedOptionIds = null) =>
        FindReferences(connection, transaction,
            well => (well.WellFeatureAssignments ?? [])
                .Where(value => value.FeatureCategoryID == categoryId &&
                    (permittedOptionIds == null || value.FeatureOptionID is Guid optionId && !permittedOptionIds.Contains(optionId)))
                .Any(),
            permittedOptionIds == null ? "WellFeatureAssignments.FeatureCategoryID" : "WellFeatureAssignments.FeatureOptionID",
            permittedOptionIds == null ? "catalog_in_use" : "catalog_option_in_use",
            permittedOptionIds == null
                ? "The feature category is referenced by one or more Wells."
                : "The update removes a feature option referenced by one or more Wells.");

    public static WellMutationError? FindIdentityReferences(SqliteConnection connection, SqliteTransaction transaction, Guid identityId) =>
        FindReferences(connection, transaction,
            well => (well.WellIdentityAssignments ?? []).Any(value => value.IdentityID == identityId),
            "WellIdentityAssignments.IdentityID", "catalog_in_use",
            "The Well identity is referenced by one or more Wells.");

    private static void ValidateFeatureAssignment(WellFeatureAssignment assignment,
        IReadOnlyDictionary<Guid, CategoryDefinition> categories, string path, List<WellMutationError> errors)
    {
        if (assignment.FeatureCategoryID is not Guid category || category == Guid.Empty)
        {
            errors.Add(Error($"{path}.FeatureCategoryID", "category_id_required", "A non-empty category UUID is required."));
            return;
        }
        if (!categories.TryGetValue(category, out CategoryDefinition? definition))
        {
            errors.Add(Error($"{path}.FeatureCategoryID", "category_not_found", $"No local category has UUID {category}."));
            return;
        }
        if (assignment.FeatureOptionID is not Guid option || option == Guid.Empty)
        {
            errors.Add(Error($"{path}.FeatureOptionID", "option_id_required", "A non-empty option UUID is required."));
            return;
        }
        if (!definition.Options.Contains(option))
        {
            errors.Add(Error($"{path}.FeatureOptionID", "option_not_in_category", $"Option UUID {option} does not belong to category UUID {category}."));
        }
        if (assignment.FromDate > assignment.ToDate)
            errors.Add(Error($"{path}.FromDate", "invalid_validity_period", "FromDate must be earlier than or equal to ToDate."));
        if (!definition.HasValidityPeriod && (assignment.FromDate is not null || assignment.ToDate is not null))
            errors.Add(Error(path, "validity_period_not_allowed", "This category does not support a validity period."));
    }

    private static void ValidateRequiredReference(Guid? id, IReadOnlySet<Guid> knownIds, string property,
        string code, List<WellMutationError> errors)
    {
        if (id is not Guid value || value == Guid.Empty)
        {
            errors.Add(Error(property, "identity_id_required", "A non-empty identity UUID is required."));
            return;
        }
        if (!knownIds.Contains(value))
        {
            errors.Add(Error(property, code, $"No local catalog definition has UUID {id}."));
        }
    }

    private static void ValidateAssignmentId(Guid id, HashSet<Guid> knownIds, string property, List<WellMutationError> errors)
    {
        if (id == Guid.Empty)
            errors.Add(Error(property, "assignment_id_required", "A non-empty assignment UUID is required."));
        else if (!knownIds.Add(id))
            errors.Add(Error(property, "duplicate_assignment_id", $"Assignment UUID {id} is used more than once."));
    }

    private static void ValidateExclusiveCategoryPeriods(IReadOnlyCollection<WellFeatureAssignment> assignments,
        IReadOnlyDictionary<Guid, CategoryDefinition> categories, List<WellMutationError> errors)
    {
        foreach (IGrouping<Guid, WellFeatureAssignment> group in assignments
            .Where(value => value is not null && value.FeatureCategoryID is Guid)
            .GroupBy(value => value.FeatureCategoryID!.Value))
        {
            if (!categories.TryGetValue(group.Key, out CategoryDefinition? definition) || !definition.IsExclusive)
                continue;
            WellFeatureAssignment[] values = group.ToArray();
            for (int left = 0; left < values.Length; left++)
            for (int right = left + 1; right < values.Length; right++)
            {
                if (!definition.HasValidityPeriod || PeriodsOverlap(values[left], values[right]))
                    errors.Add(Error("WellFeatureAssignments", "exclusive_category_overlap",
                        $"Exclusive category UUID {group.Key} has assignments with overlapping validity periods."));
            }
        }
    }

    private static bool PeriodsOverlap(WellFeatureAssignment left, WellFeatureAssignment right) =>
        (left.ToDate is null || right.FromDate is null || left.ToDate >= right.FromDate) &&
        (right.ToDate is null || left.FromDate is null || right.ToDate >= left.FromDate);

    private static WellMutationError? FindReferences(SqliteConnection connection, SqliteTransaction transaction,
        Func<Model.Well, bool> predicate, string property, string code, string message)
    {
        List<Guid> wellIds = ReadWells(connection, transaction)
            .Where(pair => predicate(pair.Value))
            .Select(pair => pair.Key)
            .Distinct()
            .OrderBy(value => value)
            .ToList();
        return wellIds.Count == 0
            ? null
            : new WellMutationError { Property = property, Code = code, Message = message, ReferencingWellIDs = wellIds };
    }

    private static Dictionary<Guid, Model.Well> ReadWells(SqliteConnection connection, SqliteTransaction transaction)
    {
        Dictionary<Guid, Model.Well> result = [];
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT ID, Well FROM WellTable";
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            Model.Well? well = JsonSerializer.Deserialize<Model.Well>(reader.GetString(1), JsonSettings.Options);
            if (well != null)
            {
                result[reader.GetGuid(0)] = well;
            }
        }
        return result;
    }

    private static HashSet<Guid> ReadDefinitionIds<T>(SqliteConnection connection, SqliteTransaction transaction,
        string table, string column, Func<T, Guid?> idSelector)
    {
        HashSet<Guid> result = [];
        foreach (T value in ReadDocuments<T>(connection, transaction, table, column))
        {
            if (idSelector(value) is Guid id && id != Guid.Empty)
            {
                result.Add(id);
            }
        }
        return result;
    }

    private static Dictionary<Guid, CategoryDefinition> ReadCategoryDefinitions(
        SqliteConnection connection, SqliteTransaction transaction)
    {
        Dictionary<Guid, CategoryDefinition> result = [];
        foreach (WellFeatureCategory category in ReadDocuments<WellFeatureCategory>(connection, transaction,
            "WellFeatureCategoryTable", "WellFeatureCategory"))
        {
            if (category.MetaInfo?.ID is not Guid id || id == Guid.Empty)
            {
                continue;
            }
            result[id] = new CategoryDefinition(category.IsExclusive, category.HasValidityPeriod,
                (category.Options ?? []).Select(value => value.ID).Where(value => value != Guid.Empty).ToHashSet());
        }
        return result;
    }

    private static List<T> ReadDocuments<T>(SqliteConnection connection, SqliteTransaction transaction, string table, string column)
    {
        List<T> result = [];
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT {column} FROM {table}";
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            T? value = JsonSerializer.Deserialize<T>(reader.GetString(0), JsonSettings.Options);
            if (value != null)
            {
                result.Add(value);
            }
        }
        return result;
    }

    private static WellMutationError Error(string property, string code, string message) =>
        new() { Property = property, Code = code, Message = message };
}
