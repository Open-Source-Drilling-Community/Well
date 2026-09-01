using Microsoft.Data.Sqlite;
using OSDC.Drilling.Well.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace OSDC.Drilling.Well.Service.Managers;

internal static class WellReferenceIntegrityValidator
{
    public static List<WellMutationError> ValidateWell(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Model.Well well)
    {
        Dictionary<Guid, HashSet<Guid>> featureOptions = ReadCategoryOptions<WellFeatureCategory, WellFeatureOption>(
            connection, transaction, "WellFeatureCategoryTable", "WellFeatureCategory",
            category => category.MetaInfo?.ID, category => category.Options, option => option.ID);
        HashSet<Guid> identities = ReadDefinitionIds<WellIdentity>(connection, transaction, "WellIdentityTable", "WellIdentity", value => value.MetaInfo?.ID);

        List<WellMutationError> errors = [];
        for (int index = 0; index < (well.WellFeatureAssignments?.Count ?? 0); index++)
        {
            WellFeatureAssignment assignment = well.WellFeatureAssignments![index];
            ValidateCategoryReference(assignment.FeatureCategoryID, assignment.FeatureOptionID, featureOptions,
                $"WellFeatureAssignments[{index}]", "FeatureCategoryID", "FeatureOptionID", errors);
        }
        for (int index = 0; index < (well.WellIdentityAssignments?.Count ?? 0); index++)
        {
            Guid? id = well.WellIdentityAssignments![index].IdentityID;
            ValidateOptionalReference(id, identities, $"WellIdentityAssignments[{index}].IdentityID", "well_identity_not_found", errors);
        }
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

    private static void ValidateCategoryReference(Guid? categoryId, Guid? optionId,
        IReadOnlyDictionary<Guid, HashSet<Guid>> optionsByCategory, string path, string categoryProperty,
        string optionProperty, List<WellMutationError> errors)
    {
        if (categoryId == null && optionId == null)
        {
            return;
        }
        if (categoryId is not Guid category || category == Guid.Empty)
        {
            errors.Add(Error($"{path}.{categoryProperty}", "category_id_required", "A category UUID is required when an option is selected."));
            return;
        }
        if (!optionsByCategory.TryGetValue(category, out HashSet<Guid>? options))
        {
            errors.Add(Error($"{path}.{categoryProperty}", "category_not_found", $"No local category has UUID {category}."));
            return;
        }
        if (optionId is not Guid option || option == Guid.Empty)
        {
            errors.Add(Error($"{path}.{optionProperty}", "option_id_required", "An option UUID is required when a category is selected."));
            return;
        }
        if (!options.Contains(option))
        {
            errors.Add(Error($"{path}.{optionProperty}", "option_not_in_category", $"Option UUID {option} does not belong to category UUID {category}."));
        }
    }

    private static void ValidateOptionalReference(Guid? id, IReadOnlySet<Guid> knownIds, string property,
        string code, List<WellMutationError> errors)
    {
        if (id == null)
        {
            return;
        }
        if (id == Guid.Empty || !knownIds.Contains(id.Value))
        {
            errors.Add(Error(property, code, $"No local catalog definition has UUID {id}."));
        }
    }

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

    private static Dictionary<Guid, HashSet<Guid>> ReadCategoryOptions<TCategory, TOption>(
        SqliteConnection connection, SqliteTransaction transaction, string table, string column,
        Func<TCategory, Guid?> categoryId, Func<TCategory, List<TOption>?> options,
        Func<TOption, Guid> optionId)
    {
        Dictionary<Guid, HashSet<Guid>> result = [];
        foreach (TCategory category in ReadDocuments<TCategory>(connection, transaction, table, column))
        {
            if (categoryId(category) is not Guid id || id == Guid.Empty)
            {
                continue;
            }
            result[id] = (options(category) ?? []).Select(optionId).Where(value => value != Guid.Empty).ToHashSet();
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
