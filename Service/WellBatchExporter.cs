using OSDC.Drilling.Well.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OSDC.Drilling.Well.Service;

public enum WellBatchExportFailureKind { None, InvalidRequest, WellNotFound, StorageFailure }

public sealed class WellBatchExportOutcome
{
    public WellBatchExportDocument? Document { get; init; }
    public WellBatchErrorEnvelope? Error { get; init; }
    public WellBatchExportFailureKind FailureKind { get; init; }
    public bool IsSuccess => Document != null && FailureKind == WellBatchExportFailureKind.None;
}

public static class WellBatchExporter
{
    public static WellBatchExportOutcome Create(WellBatchExportRequest? request,
        IEnumerable<Model.Well?> snapshot, DateTimeOffset exportedAtUtc,
        IEnumerable<WellIdentity> identities, IEnumerable<WellFeatureCategory> categories)
    {
        List<WellBatchError> errors = ValidateRequest(request);
        if (errors.Count != 0) return Failure(WellBatchExportFailureKind.InvalidRequest,
            "invalid_batch_export_request", "The Well batch-export request is invalid.", errors);

        Dictionary<Guid, Model.Well> byId = [];
        int position = 0;
        foreach (Model.Well? well in snapshot)
        {
            Guid? id = well?.MetaInfo?.ID;
            if (well == null || id == null || id == Guid.Empty || !byId.TryAdd(id.Value, well))
                return Failure(WellBatchExportFailureKind.StorageFailure, "well_export_failed",
                    "A stored Well could not be represented in the export.",
                    [Error(position, "Wells", "invalid_stored_well", "A stored Well is null, has no UUID, or duplicates another UUID.")]);
            position++;
        }

        List<Model.Well> selected;
        if (request!.Scope == WellBatchExportScope.All)
            selected = byId.OrderBy(pair => pair.Key).Select(pair => pair.Value).ToList();
        else
        {
            selected = [];
            for (int index = 0; index < request.WellIDs!.Count; index++)
            {
                Guid id = request.WellIDs[index];
                if (byId.TryGetValue(id, out Model.Well? well)) selected.Add(well);
                else errors.Add(Error(index, "WellIDs", "well_not_found", $"No stored Well has UUID '{id}'."));
            }
            if (errors.Count != 0) return Failure(WellBatchExportFailureKind.WellNotFound,
                "well_not_found", "One or more selected Wells do not exist.", errors);
        }

        WellBatchCatalogDependencies dependencies = BuildDependencies(selected, identities, categories, errors);
        if (errors.Count != 0) return Failure(WellBatchExportFailureKind.StorageFailure,
            "well_export_dependency_missing", "The export could not include every referenced local catalog definition.", errors);

        return new WellBatchExportOutcome
        {
            Document = new WellBatchExportDocument
            {
                ExportedAtUtc = exportedAtUtc.ToUniversalTime(),
                CatalogDependencies = dependencies,
                Wells = selected
            }
        };
    }

    public static WellBatchExportOutcome StorageFailure(string message) => Failure(
        WellBatchExportFailureKind.StorageFailure, "well_export_failed", message,
        [Error(null, "Document", "storage_failure", "The export snapshot could not be produced.")]);

    private static WellBatchCatalogDependencies BuildDependencies(IReadOnlyList<Model.Well> wells,
        IEnumerable<WellIdentity> identities, IEnumerable<WellFeatureCategory> categories,
        List<WellBatchError> errors)
    {
        Dictionary<Guid, WellIdentity> identityIndex = identities
            .Where(value => value?.MetaInfo?.ID is Guid id && id != Guid.Empty)
            .GroupBy(value => value.MetaInfo!.ID).ToDictionary(group => group.Key, group => group.First());
        Dictionary<Guid, WellFeatureCategory> categoryIndex = categories
            .Where(value => value?.MetaInfo?.ID is Guid id && id != Guid.Empty)
            .GroupBy(value => value.MetaInfo!.ID).ToDictionary(group => group.Key, group => group.First());
        HashSet<Guid> identityIds = [];
        Dictionary<Guid, HashSet<Guid>> optionIdsByCategory = [];

        for (int index = 0; index < wells.Count; index++)
        {
            foreach (WellIdentityAssignment? assignment in wells[index].WellIdentityAssignments ?? [])
            {
                if (assignment?.IdentityID is Guid id && id != Guid.Empty) identityIds.Add(id);
                else errors.Add(Error(index, "Wells.WellIdentityAssignments.IdentityID", "invalid_catalog_reference", "Identity references must be non-empty UUIDs."));
            }
            foreach (WellFeatureAssignment? assignment in wells[index].WellFeatureAssignments ?? [])
            {
                if (assignment?.FeatureCategoryID is not Guid categoryId || categoryId == Guid.Empty ||
                    assignment.FeatureOptionID is not Guid optionId || optionId == Guid.Empty)
                {
                    errors.Add(Error(index, "Wells.WellFeatureAssignments", "invalid_catalog_reference", "Feature category and option references must be non-empty UUIDs."));
                    continue;
                }
                if (!optionIdsByCategory.TryGetValue(categoryId, out HashSet<Guid>? optionIds))
                    optionIdsByCategory.Add(categoryId, optionIds = []);
                optionIds.Add(optionId);
            }
        }

        WellBatchCatalogDependencies result = new();
        foreach (Guid id in identityIds.Order())
        {
            if (identityIndex.TryGetValue(id, out WellIdentity? identity)) result.Identities.Add(identity);
            else errors.Add(Error(null, "CatalogDependencies.Identities", "referenced_definition_missing", $"Referenced identity '{id}' does not exist."));
        }
        foreach ((Guid categoryId, HashSet<Guid> requiredOptions) in optionIdsByCategory.OrderBy(pair => pair.Key))
        {
            if (!categoryIndex.TryGetValue(categoryId, out WellFeatureCategory? category))
            {
                errors.Add(Error(null, "CatalogDependencies.FeatureCategories", "referenced_definition_missing", $"Referenced feature category '{categoryId}' does not exist."));
                continue;
            }
            Dictionary<Guid, WellFeatureOption> available = (category.Options ?? []).Where(value => value.ID != Guid.Empty)
                .GroupBy(value => value.ID).ToDictionary(group => group.Key, group => group.First());
            List<WellFeatureOption> options = [];
            foreach (Guid optionId in requiredOptions.Order())
            {
                if (available.TryGetValue(optionId, out WellFeatureOption? option)) options.Add(option);
                else errors.Add(Error(null, "CatalogDependencies.FeatureCategories.Options", "referenced_option_missing",
                    $"Referenced option '{optionId}' does not exist in category '{categoryId}'."));
            }
            result.FeatureCategories.Add(new WellFeatureCategory
            {
                MetaInfo = category.MetaInfo, Name = category.Name, IsExclusive = category.IsExclusive,
                HasValidityPeriod = category.HasValidityPeriod, Options = options,
                CreationDate = category.CreationDate, LastModificationDate = category.LastModificationDate
            });
        }
        return result;
    }

    private static List<WellBatchError> ValidateRequest(WellBatchExportRequest? request)
    {
        if (request == null) return [Error(null, "Request", "required", "A batch-export request is required.")];
        List<WellBatchError> errors = [];
        if (request.Scope == WellBatchExportScope.All)
        {
            if (request.WellIDs is { Count: > 0 }) errors.Add(Error(null, "WellIDs", "forbidden", "WellIDs must be omitted for an All export."));
        }
        else if (request.Scope == WellBatchExportScope.Selected)
        {
            if (request.WellIDs == null || request.WellIDs.Count == 0) errors.Add(Error(null, "WellIDs", "required", "Selected export requires at least one UUID."));
            else
            {
                HashSet<Guid> ids = [];
                for (int index = 0; index < request.WellIDs.Count; index++)
                {
                    Guid id = request.WellIDs[index];
                    if (id == Guid.Empty) errors.Add(Error(index, "WellIDs", "empty_uuid", "Well UUIDs must be non-empty."));
                    else if (!ids.Add(id)) errors.Add(Error(index, "WellIDs", "duplicate_uuid", $"Well UUID '{id}' occurs more than once."));
                }
            }
        }
        else errors.Add(Error(null, "Scope", "invalid_scope", "Scope must be All or Selected."));
        return errors;
    }

    private static WellBatchExportOutcome Failure(WellBatchExportFailureKind kind, string error,
        string message, List<WellBatchError> errors) => new()
        { FailureKind = kind, Error = new() { Error = error, Message = message, Errors = errors } };
    private static WellBatchError Error(int? index, string property, string code, string message) =>
        new() { PositionIndex = index, Property = property, Code = code, Message = message };
}
