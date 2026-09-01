using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OSDC.Drilling.Well.Service.Controllers;
using OSDC.Drilling.Well.Service.Managers;
using WellModel = OSDC.Drilling.Well.Model.Well;
using WellBatchExportRequestModel = OSDC.Drilling.Well.Model.WellBatchExportRequest;
using WellBatchRestoreRequestModel = OSDC.Drilling.Well.Model.WellBatchRestoreRequest;
using WellIdentityModel = OSDC.Drilling.Well.Model.WellIdentity;
using WellFeatureCategoryModel = OSDC.Drilling.Well.Model.WellFeatureCategory;

namespace OSDC.Drilling.Well.Service.Mcp.Tools;

public static class WellRestMcpToolRegistrations
{
    private static readonly JsonSerializerOptions StrictInputOptions = new(JsonSettings.Options)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static IServiceCollection AddWellRestMcpTools(this IServiceCollection services)
    {
        services.AddLegacyMcpTool("well_get_all_ids", "List the identifiers of every stored well. Use this lightweight operation when only UUIDs are needed. On success, data contains an array of UUID strings; the response also contains an HTTP-style status code.", McpToolArgumentHelpers.CreateEmptySchema(), McpToolArgumentHelpers.CreateIdsOutputSchema(), new("List Well UUIDs", true, false, true, false),
            (sp, args, ct) => InvokeNoArguments(args, ct, () => Controller(sp).GetAllWellId()));
        services.AddLegacyMcpTool("well_get_all_meta_info", "List identity and HTTP location metadata for every stored well without returning complete well records. On success, data contains MetaInfo objects with ID and optional HttpHostName, HttpHostBasePath, and HttpEndPoint fields.", McpToolArgumentHelpers.CreateEmptySchema(), McpToolArgumentHelpers.CreateMetaInfoListOutputSchema(), new("List Well Metadata", true, false, true, false),
            (sp, args, ct) => InvokeNoArguments(args, ct, () => Controller(sp).GetAllWellMetaInfo()));
        services.AddLegacyMcpTool("well_get_by_id", "Retrieve one complete well record by UUID. On success, data contains its metadata, name, description, timestamps, slot and cluster associations, and single-well flag. Returns status 404 when no matching well exists and 400 for an empty UUID.", McpToolArgumentHelpers.CreateGuidSchema("id", "Unique identifier of the well to retrieve."), McpToolArgumentHelpers.CreateWellOutputSchema(), new("Get Well", true, false, true, false),
            (sp, args, ct) => InvokeByGuid(args, "id", ct, id => Controller(sp).GetWellById(id)));
        services.AddLegacyMcpTool("well_get_all", "Retrieve every stored well as a complete record. Use the ID or metadata listing tools instead when full data is unnecessary. On success, data contains an array of Well objects and the response contains an HTTP-style status code.", McpToolArgumentHelpers.CreateEmptySchema(), McpToolArgumentHelpers.CreateWellListOutputSchema(), new("List Wells", true, false, true, false),
            (sp, args, ct) => InvokeNoArguments(args, ct, () => Controller(sp).GetAllWell()));
        services.AddLegacyMcpTool("well_batch_export", "Create a read-only schema-version-1 JSON backup of all stored wells or an explicitly ordered selection. The result contains complete Well records and only the Well Identity and Well Feature Category definitions and options referenced by those records. Cluster and Slot identifiers remain external references. A missing or invalid selected well rejects the complete export.", McpToolArgumentHelpers.CreateWellBatchExportSchema(), McpToolArgumentHelpers.CreateWellBatchExportOutputSchema(), new("Export Wells with Catalog Dependencies", true, false, true, false),
            (sp, args, ct) => InvokeWithBodyResult<WellBatchExportRequestModel, OSDC.Drilling.Well.Model.WellBatchExportDocument>(args, "request", ct, request => Controller(sp).BatchExportWells(request)));
        services.AddLegacyMcpTool("well_batch_restore", "Validate and atomically restore a schema-version-1 Well backup document. Source catalog UUIDs map to compatible local definitions by exact UUID or unique normalized name; MapOrCreateMissing can create missing definitions and options. ReplaceExisting can replace matching Well UUIDs. Catalog mapping, reference rewriting, catalog creation, and all Well writes use one transaction, so a validation, conflict, or storage failure changes nothing.", McpToolArgumentHelpers.CreateWellBatchRestoreSchema(), McpToolArgumentHelpers.CreateWellBatchRestoreOutputSchema(), new("Restore Wells and Catalog Dependencies", false, true, false, false),
            (sp, args, ct) => InvokeWithBodyResult<WellBatchRestoreRequestModel, OSDC.Drilling.Well.Model.WellBatchRestoreResponse>(args, "request", ct, request => Controller(sp).BatchRestoreWells(request)));
        services.AddLegacyMcpTool("well_get_all_by_slot_id", "Retrieve complete records for all wells assigned to one slot UUID. On success, data is an array of Well objects; an empty array means that no wells currently use the slot.", McpToolArgumentHelpers.CreateGuidSchema("slotId", "Identifier of the slot whose wells should be returned."), McpToolArgumentHelpers.CreateWellListOutputSchema(), new("List Wells by Slot", true, false, true, false),
            (sp, args, ct) => InvokeByGuid(args, "slotId", ct, id => Controller(sp).GetAllWellBySlotId(id)));
        services.AddLegacyMcpTool("well_get_all_by_cluster_id", "Retrieve complete records for all wells assigned to one cluster UUID. On success, data is an array of Well objects; an empty array means that the cluster currently has no wells.", McpToolArgumentHelpers.CreateGuidSchema("clusterId", "Identifier of the cluster whose wells should be returned."), McpToolArgumentHelpers.CreateWellListOutputSchema(), new("List Wells by Cluster", true, false, true, false),
            (sp, args, ct) => InvokeByGuid(args, "clusterId", ct, id => Controller(sp).GetAllWellByClusterId(id)));
        services.AddLegacyMcpTool("well_get_used_slot_meta_info_by_cluster_id", "List the slot UUIDs referenced by wells in one cluster. Use this to determine which cluster slots are already occupied without retrieving every well. Returns 404 when no matching data is found.", McpToolArgumentHelpers.CreateGuidSchema("clusterId", "Identifier of the cluster for which used-slot UUIDs should be returned."), McpToolArgumentHelpers.CreateIdsOutputSchema(), new("List Used Slot UUIDs", true, false, true, false),
            (sp, args, ct) => InvokeByGuid(args, "clusterId", ct, id => Controller(sp).GetAllUsedSlotMetaInfoByClusterId(id)));
        services.AddLegacyMcpTool("well_create", "Create and persist a new well. Supply the complete Well object using the documented PascalCase fields; well.MetaInfo.ID must be a caller-generated, non-empty UUID and must not already exist. The service does not generate an ID. Returns status 200 on success, 400 for malformed data, and 409 when the ID already exists.", McpToolArgumentHelpers.CreateWellSchema(), McpToolArgumentHelpers.CreateStatusOnlyOutputSchema(), new("Create Well", false, false, false, false),
            (sp, args, ct) => InvokeWithBody<WellModel>(args, "well", ct, data => Controller(sp).PostWell(data)));
        services.AddLegacyMcpTool("well_update_by_id", "Replace the stored data for an existing well. The top-level id and well.MetaInfo.ID must be the same non-empty UUID; expectedModifiedUtc must equal the LastModificationDate from the latest read. Include the complete desired Well object because this is a full update. A stale revision returns 409 without changing data.", McpToolArgumentHelpers.CreateWellSchema(includeId: true), McpToolArgumentHelpers.CreateStatusOnlyOutputSchema(), new("Update Well", false, true, true, false),
            (sp, args, ct) => InvokeWithIdTimestampAndBody<WellModel>(args, "well", ct, (id, expected, data) => Controller(sp).PutWellById(id, expected, data)));
        services.AddLegacyMcpTool("well_delete_by_id", "Permanently delete one stored well by UUID. Confirm the target identifier before calling because this operation removes the record. Returns status 200 on success, 404 when the well does not exist, and 400 for an empty UUID.", McpToolArgumentHelpers.CreateGuidSchema("id", "Unique identifier of the well to delete."), McpToolArgumentHelpers.CreateStatusOnlyOutputSchema(), new("Delete Well", false, true, true, false),
            (sp, args, ct) => InvokeDelete(args, ct, id => Controller(sp).DeleteWellById(id)));
        AddCatalogCrudTools<WellIdentityModel>(
            services, "well_identity", "wellIdentity", "Well Identity", "a symbolic identity definition assignable to Wells",
            McpToolArgumentHelpers.CreateWellIdentitySchema, McpToolArgumentHelpers.CreateWellIdentityResourceSchema,
            sp => IdentityController(sp).GetAllWellIdentityId(),
            sp => IdentityController(sp).GetAllWellIdentityMetaInfo(),
            (sp, id) => IdentityController(sp).GetWellIdentityById(id),
            sp => IdentityController(sp).GetAllWellIdentity(),
            (sp, data) => IdentityController(sp).PostWellIdentity(data),
            (sp, id, expected, data) => IdentityController(sp).PutWellIdentityById(id, expected, data),
            (sp, id) => IdentityController(sp).DeleteWellIdentityById(id));
        AddCatalogCrudTools<WellFeatureCategoryModel>(
            services, "well_feature_category", "wellFeatureCategory", "Well Feature Category", "a definition of allowed feature options assignable to Wells",
            McpToolArgumentHelpers.CreateWellFeatureCategorySchema, McpToolArgumentHelpers.CreateWellFeatureCategoryResourceSchema,
            sp => FeatureCategoryController(sp).GetAllWellFeatureCategoryId(),
            sp => FeatureCategoryController(sp).GetAllWellFeatureCategoryMetaInfo(),
            (sp, id) => FeatureCategoryController(sp).GetWellFeatureCategoryById(id),
            sp => FeatureCategoryController(sp).GetAllWellFeatureCategory(),
            (sp, data) => FeatureCategoryController(sp).PostWellFeatureCategory(data),
            (sp, id, expected, data) => FeatureCategoryController(sp).PutWellFeatureCategoryById(id, expected, data),
            (sp, id) => FeatureCategoryController(sp).DeleteWellFeatureCategoryById(id));
        return services;
    }

    private static void AddCatalogCrudTools<TModel>(IServiceCollection services, string prefix, string bodyName,
        string entityName, string purpose, Func<bool, JsonObject> inputSchema,
        Func<JsonObject> resourceSchema, Func<IServiceProvider, ActionResult<System.Collections.Generic.IEnumerable<Guid>>> getIds,
        Func<IServiceProvider, ActionResult<System.Collections.Generic.IEnumerable<OSDC.DotnetLibraries.General.DataManagement.MetaInfo?>>> getMetaInfo,
        Func<IServiceProvider, Guid, ActionResult<TModel?>> getById,
        Func<IServiceProvider, ActionResult<System.Collections.Generic.IEnumerable<TModel?>>> getAll,
        Func<IServiceProvider, TModel?, ActionResult> create,
        Func<IServiceProvider, Guid, DateTimeOffset, TModel?, ActionResult> update,
        Func<IServiceProvider, Guid, ActionResult> delete)
    {
        services.AddLegacyMcpTool($"{prefix}_get_all_ids", $"List the UUID of every stored {entityName} without transferring complete definitions. Each UUID identifies {purpose} and can be supplied to the corresponding get-by-ID operation.", McpToolArgumentHelpers.CreateEmptySchema(), McpToolArgumentHelpers.CreateIdsOutputSchema(), new($"List {entityName} UUIDs", true, false, true, false),
            (sp, args, ct) => InvokeNoArguments(args, ct, () => getIds(sp)));
        services.AddLegacyMcpTool($"{prefix}_get_all_meta_info", $"List identity and optional HTTP location metadata for every stored {entityName} without returning complete definitions. Use this discovery operation when names and options are unnecessary.", McpToolArgumentHelpers.CreateEmptySchema(), McpToolArgumentHelpers.CreateMetaInfoListOutputSchema(), new($"List {entityName} Metadata", true, false, true, false),
            (sp, args, ct) => InvokeNoArguments(args, ct, () => getMetaInfo(sp)));
        services.AddLegacyMcpTool($"{prefix}_get_by_id", $"Retrieve one complete {entityName} by UUID. The returned resource represents {purpose}. Returns HTTP-style status 404 when absent and validation status 400 for a missing, malformed, or empty UUID.", McpToolArgumentHelpers.CreateGuidSchema("id", $"Non-empty UUID of the {entityName} to retrieve."), McpToolArgumentHelpers.CreateResourceOutputSchema(resourceSchema()), new($"Get {entityName}", true, false, true, false),
            (sp, args, ct) => InvokeByGuid(args, "id", ct, id => getById(sp, id)));
        services.AddLegacyMcpTool($"{prefix}_get_all", $"Retrieve every stored {entityName} as a complete definition. Each result represents {purpose}; prefer the ID or metadata listing tools when complete content is unnecessary.", McpToolArgumentHelpers.CreateEmptySchema(), McpToolArgumentHelpers.CreateResourceListOutputSchema(resourceSchema()), new($"List {entityName} Definitions", true, false, true, false),
            (sp, args, ct) => InvokeNoArguments(args, ct, () => getAll(sp)));
        services.AddLegacyMcpTool($"{prefix}_create", $"Create and persist {purpose}. Supply the complete {bodyName} object with a caller-generated non-empty MetaInfo.ID and a non-blank name. The UUID must not already exist; feature-option UUIDs must be non-empty and unique within a category.", inputSchema(false), McpToolArgumentHelpers.CreateResourceOutputSchema(resourceSchema()), new($"Create {entityName}", false, false, false, false),
            (sp, args, ct) => InvokeWithBody<TModel>(args, bodyName, ct, data => create(sp, data)));
        services.AddLegacyMcpTool($"{prefix}_update_by_id", $"Replace an existing {entityName} with the complete supplied definition. The top-level id must equal {bodyName}.MetaInfo.ID, and expectedModifiedUtc must equal the latest LastModificationDate. Removing a definition or option referenced by a Well is rejected atomically with a conflict.", inputSchema(true), McpToolArgumentHelpers.CreateResourceOutputSchema(resourceSchema()), new($"Update {entityName}", false, true, true, false),
            (sp, args, ct) => InvokeWithIdTimestampAndBody<TModel>(args, bodyName, ct, (id, expected, data) => update(sp, id, expected, data)));
        services.AddLegacyMcpTool($"{prefix}_delete_by_id", $"Permanently delete one stored {entityName} by non-empty UUID. Deletion is rejected with a conflict while any stored Well references the definition or one of its feature options; the service performs no cascading deletion.", McpToolArgumentHelpers.CreateGuidSchema("id", $"Non-empty UUID of the {entityName} to delete."), McpToolArgumentHelpers.CreateStatusOnlyOutputSchema(), new($"Delete {entityName}", false, true, true, false),
            (sp, args, ct) => InvokeDelete(args, ct, id => delete(sp, id)));
    }

    private static Task<JsonNode?> Invoke<T>(CancellationToken ct, Func<ActionResult<T>> action)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<JsonNode?>(McpActionResultConverter.FromActionResult(action()));
    }

    private static Task<JsonNode?> InvokeNoArguments<T>(JsonObject? args, CancellationToken ct, Func<ActionResult<T>> action)
    {
        if (!HasOnlyArguments(args, out JsonNode? error)) return Task.FromResult(error);
        return Invoke(ct, action);
    }

    private static Task<JsonNode?> InvokeByGuid<T>(JsonObject? args, string key, CancellationToken ct, Func<Guid, ActionResult<T>> action)
    {
        ct.ThrowIfCancellationRequested();
        if (!HasOnlyArguments(args, out JsonNode? unexpected, key)) return Task.FromResult(unexpected);
        return McpToolArgumentHelpers.TryParseGuid(args, key, out Guid id, out JsonNode? error)
            ? Task.FromResult<JsonNode?>(McpActionResultConverter.FromActionResult(action(id)))
            : Task.FromResult(error);
    }

    private static Task<JsonNode?> InvokeDelete(JsonObject? args, CancellationToken ct, Func<Guid, ActionResult> action)
    {
        ct.ThrowIfCancellationRequested();
        if (!HasOnlyArguments(args, out JsonNode? unexpected, "id")) return Task.FromResult(unexpected);
        return McpToolArgumentHelpers.TryParseGuid(args, "id", out Guid id, out JsonNode? error)
            ? Task.FromResult<JsonNode?>(McpActionResultConverter.FromActionResult(action(id)))
            : Task.FromResult(error);
    }

    private static Task<JsonNode?> InvokeWithBody<T>(JsonObject? args, string bodyName, CancellationToken ct, Func<T?, ActionResult> action)
    {
        ct.ThrowIfCancellationRequested();
        if (!HasOnlyArguments(args, out JsonNode? unexpected, bodyName)) return Task.FromResult(unexpected);
        return TryDeserialize(args, bodyName, out T? data, out JsonNode? error)
            ? Task.FromResult<JsonNode?>(McpActionResultConverter.FromActionResult(action(data)))
            : Task.FromResult(error);
    }

    private static Task<JsonNode?> InvokeWithBodyResult<TModel, TResult>(JsonObject? args, string bodyName, CancellationToken ct, Func<TModel?, ActionResult<TResult>> action)
    {
        ct.ThrowIfCancellationRequested();
        if (!HasOnlyArguments(args, out JsonNode? unexpected, bodyName)) return Task.FromResult(unexpected);
        return TryDeserialize(args, bodyName, out TModel? data, out JsonNode? error)
            ? Task.FromResult<JsonNode?>(McpActionResultConverter.FromActionResult(action(data)))
            : Task.FromResult(error);
    }

    private static Task<JsonNode?> InvokeWithIdAndBody<T>(JsonObject? args, string bodyName, CancellationToken ct, Func<Guid, T?, ActionResult> action)
    {
        ct.ThrowIfCancellationRequested();
        if (!HasOnlyArguments(args, out JsonNode? unexpected, "id", bodyName)) return Task.FromResult(unexpected);
        if (!McpToolArgumentHelpers.TryParseGuid(args, "id", out Guid id, out JsonNode? idError)) return Task.FromResult(idError);
        return TryDeserialize(args, bodyName, out T? data, out JsonNode? error)
            ? Task.FromResult<JsonNode?>(McpActionResultConverter.FromActionResult(action(id, data)))
            : Task.FromResult(error);
    }

    private static Task<JsonNode?> InvokeWithIdTimestampAndBody<T>(JsonObject? args, string bodyName, CancellationToken ct, Func<Guid, DateTimeOffset, T?, ActionResult> action)
    {
        ct.ThrowIfCancellationRequested();
        if (!HasOnlyArguments(args, out JsonNode? unexpected, "id", "expectedModifiedUtc", bodyName)) return Task.FromResult(unexpected);
        if (!McpToolArgumentHelpers.TryParseGuid(args, "id", out Guid id, out JsonNode? idError)) return Task.FromResult(idError);
        if (!McpToolArgumentHelpers.TryParseDateTimeOffset(args, "expectedModifiedUtc", out DateTimeOffset expected, out JsonNode? timestampError)) return Task.FromResult(timestampError);
        return TryDeserialize(args, bodyName, out T? data, out JsonNode? error)
            ? Task.FromResult<JsonNode?>(McpActionResultConverter.FromActionResult(action(id, expected, data)))
            : Task.FromResult(error);
    }

    private static bool TryDeserialize<T>(JsonObject? args, string bodyName, out T? data, out JsonNode? error)
    {
        data = default;
        error = null;
        if (args?[bodyName] is not JsonNode node)
        {
            error = McpToolResponses.CreateValidationError($"Argument '{bodyName}' is required.");
            return false;
        }
        try
        {
            data = node.Deserialize<T>(StrictInputOptions);
            if (data is null) throw new InvalidOperationException();
            return true;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or NotSupportedException)
        {
            error = McpToolResponses.CreateValidationError($"Argument '{bodyName}' could not be deserialized.");
            return false;
        }
    }

    private static bool HasOnlyArguments(JsonObject? args, out JsonNode? error, params string[] allowed)
    {
        error = null;
        if (args is null) return true;
        var allowedNames = new System.Collections.Generic.HashSet<string>(allowed, StringComparer.Ordinal);
        foreach (var argument in args)
        {
            if (!allowedNames.Contains(argument.Key))
            {
                error = McpToolResponses.CreateValidationError($"Unexpected argument '{argument.Key}'.");
                return false;
            }
        }
        return true;
    }

    private static WellController Controller(IServiceProvider sp) => new(
        sp.GetRequiredService<ILogger<WellManager>>(),
        sp.GetRequiredService<SqlConnectionManager>());

    private static WellIdentityController IdentityController(IServiceProvider sp) => new(
        sp.GetRequiredService<ILogger<WellIdentityManager>>(), sp.GetRequiredService<SqlConnectionManager>());

    private static WellFeatureCategoryController FeatureCategoryController(IServiceProvider sp) => new(
        sp.GetRequiredService<ILogger<WellFeatureCategoryManager>>(), sp.GetRequiredService<SqlConnectionManager>());
}
