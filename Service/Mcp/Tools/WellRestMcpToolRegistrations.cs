using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NORCE.Drilling.Well.Service.Controllers;
using NORCE.Drilling.Well.Service.Managers;
using WellModel = NORCE.Drilling.Well.Model.Well;

namespace NORCE.Drilling.Well.Service.Mcp.Tools;

public static class WellRestMcpToolRegistrations
{
    public static IServiceCollection AddWellRestMcpTools(this IServiceCollection services)
    {
        services.AddLegacyMcpTool("well_get_all_ids", "List the identifiers of every stored well. Use this lightweight operation when only UUIDs are needed. On success, data contains an array of UUID strings; the response also contains an HTTP-style status code.", McpToolArgumentHelpers.CreateEmptySchema(),
            (sp, _, ct) => Invoke(ct, () => Controller(sp).GetAllWellId()));
        services.AddLegacyMcpTool("well_get_all_meta_info", "List identity and HTTP location metadata for every stored well without returning complete well records. On success, data contains MetaInfo objects with ID and optional HttpHostName, HttpHostBasePath, and HttpEndPoint fields.", McpToolArgumentHelpers.CreateEmptySchema(),
            (sp, _, ct) => Invoke(ct, () => Controller(sp).GetAllWellMetaInfo()));
        services.AddLegacyMcpTool("well_get_by_id", "Retrieve one complete well record by UUID. On success, data contains its metadata, name, description, timestamps, slot and cluster associations, and single-well flag. Returns status 404 when no matching well exists and 400 for an empty UUID.", McpToolArgumentHelpers.CreateGuidSchema("id", "Unique identifier of the well to retrieve."),
            (sp, args, ct) => InvokeByGuid(args, "id", ct, id => Controller(sp).GetWellById(id)));
        services.AddLegacyMcpTool("well_get_all", "Retrieve every stored well as a complete record. Use the ID or metadata listing tools instead when full data is unnecessary. On success, data contains an array of Well objects and the response contains an HTTP-style status code.", McpToolArgumentHelpers.CreateEmptySchema(),
            (sp, _, ct) => Invoke(ct, () => Controller(sp).GetAllWell()));
        services.AddLegacyMcpTool("well_get_all_by_slot_id", "Retrieve complete records for all wells assigned to one slot UUID. On success, data is an array of Well objects; an empty array means that no wells currently use the slot.", McpToolArgumentHelpers.CreateGuidSchema("slotId", "Identifier of the slot whose wells should be returned."),
            (sp, args, ct) => InvokeByGuid(args, "slotId", ct, id => Controller(sp).GetAllWellBySlotId(id)));
        services.AddLegacyMcpTool("well_get_all_by_cluster_id", "Retrieve complete records for all wells assigned to one cluster UUID. On success, data is an array of Well objects; an empty array means that the cluster currently has no wells.", McpToolArgumentHelpers.CreateGuidSchema("clusterId", "Identifier of the cluster whose wells should be returned."),
            (sp, args, ct) => InvokeByGuid(args, "clusterId", ct, id => Controller(sp).GetAllWellByClusterId(id)));
        services.AddLegacyMcpTool("well_get_used_slot_meta_info_by_cluster_id", "List metadata for the slots referenced by wells in one cluster. Use this to determine which cluster slots are already occupied without retrieving every well. On success, data contains MetaInfo objects for the used slots; returns 404 when no matching data is found.", McpToolArgumentHelpers.CreateGuidSchema("clusterId", "Identifier of the cluster for which used-slot metadata should be returned."),
            (sp, args, ct) => InvokeByGuid(args, "clusterId", ct, id => Controller(sp).GetAllUsedSlotMetaInfoByClusterId(id)));
        services.AddLegacyMcpTool("well_create", "Create and persist a new well. Supply the complete Well object using the documented PascalCase fields; well.MetaInfo.ID must be a caller-generated, non-empty UUID and must not already exist. The service does not generate an ID. Returns status 200 on success, 400 for malformed data, and 409 when the ID already exists.", McpToolArgumentHelpers.CreateWellSchema(),
            (sp, args, ct) => InvokeWithBody<WellModel>(args, "well", ct, data => Controller(sp).PostWell(data)));
        services.AddLegacyMcpTool("well_update_by_id", "Replace the stored data for an existing well. The top-level id and well.MetaInfo.ID must be the same non-empty UUID; include the complete desired Well object because the operation is a full update rather than a partial patch. Returns status 200 on success, 400 for malformed or mismatched IDs, and 404 when the well does not exist.", McpToolArgumentHelpers.CreateWellSchema(includeId: true),
            (sp, args, ct) => InvokeWithIdAndBody<WellModel>(args, "well", ct, (id, data) => Controller(sp).PutWellById(id, data)));
        services.AddLegacyMcpTool("well_delete_by_id", "Permanently delete one stored well by UUID. Confirm the target identifier before calling because this operation removes the record. Returns status 200 on success, 404 when the well does not exist, and 400 for an empty UUID.", McpToolArgumentHelpers.CreateGuidSchema("id", "Unique identifier of the well to delete."),
            (sp, args, ct) => InvokeDelete(args, ct, id => Controller(sp).DeleteWellById(id)));
        return services;
    }

    private static Task<JsonNode?> Invoke<T>(CancellationToken ct, Func<ActionResult<T>> action)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<JsonNode?>(McpActionResultConverter.FromActionResult(action()));
    }

    private static Task<JsonNode?> InvokeByGuid<T>(JsonObject? args, string key, CancellationToken ct, Func<Guid, ActionResult<T>> action)
    {
        ct.ThrowIfCancellationRequested();
        return McpToolArgumentHelpers.TryParseGuid(args, key, out Guid id, out JsonNode? error)
            ? Task.FromResult<JsonNode?>(McpActionResultConverter.FromActionResult(action(id)))
            : Task.FromResult(error);
    }

    private static Task<JsonNode?> InvokeDelete(JsonObject? args, CancellationToken ct, Func<Guid, ActionResult> action)
    {
        ct.ThrowIfCancellationRequested();
        return McpToolArgumentHelpers.TryParseGuid(args, "id", out Guid id, out JsonNode? error)
            ? Task.FromResult<JsonNode?>(McpActionResultConverter.FromActionResult(action(id)))
            : Task.FromResult(error);
    }

    private static Task<JsonNode?> InvokeWithBody<T>(JsonObject? args, string bodyName, CancellationToken ct, Func<T?, ActionResult> action)
    {
        ct.ThrowIfCancellationRequested();
        return TryDeserialize(args, bodyName, out T? data, out JsonNode? error)
            ? Task.FromResult<JsonNode?>(McpActionResultConverter.FromActionResult(action(data)))
            : Task.FromResult(error);
    }

    private static Task<JsonNode?> InvokeWithIdAndBody<T>(JsonObject? args, string bodyName, CancellationToken ct, Func<Guid, T?, ActionResult> action)
    {
        ct.ThrowIfCancellationRequested();
        if (!McpToolArgumentHelpers.TryParseGuid(args, "id", out Guid id, out JsonNode? idError)) return Task.FromResult(idError);
        return TryDeserialize(args, bodyName, out T? data, out JsonNode? error)
            ? Task.FromResult<JsonNode?>(McpActionResultConverter.FromActionResult(action(id, data)))
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
            data = node.Deserialize<T>(JsonSettings.Options);
            if (data is null) throw new InvalidOperationException();
            return true;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            error = McpToolResponses.CreateValidationError($"Argument '{bodyName}' could not be deserialized.");
            return false;
        }
    }

    private static WellController Controller(IServiceProvider sp) => new(
        sp.GetRequiredService<ILogger<WellManager>>(),
        sp.GetRequiredService<SqlConnectionManager>());
}
