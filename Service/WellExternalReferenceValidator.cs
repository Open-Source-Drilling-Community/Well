using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using OSDC.Drilling.Well.Model;
using WellModel = OSDC.Drilling.Well.Model.Well;

namespace OSDC.Drilling.Well.Service;

public interface IWellExternalReferenceValidator
{
    Task<IReadOnlyList<WellExternalReferenceValidation>> ValidateAsync(
        IReadOnlyCollection<WellModel> wells, CancellationToken cancellationToken);
}

internal sealed class UnavailableWellExternalReferenceValidator : IWellExternalReferenceValidator
{
    public Task<IReadOnlyList<WellExternalReferenceValidation>> ValidateAsync(
        IReadOnlyCollection<WellModel> wells, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DateTimeOffset checkedAt = DateTimeOffset.UtcNow;
        IReadOnlyList<WellExternalReferenceValidation> results = wells.Select(well => new WellExternalReferenceValidation
        {
            WellID = well.MetaInfo?.ID ?? Guid.Empty,
            ClusterID = well.ClusterID,
            SlotID = well.SlotID,
            CheckedAtUtc = checkedAt,
            Status = WellExternalReferenceValidationStatus.Unavailable,
            Issues = [new WellExternalReferenceIssue
            {
                Property = "ClusterID", Code = "cluster_service_unavailable",
                Message = "Cluster reference validation is unavailable in this host."
            }]
        }).ToList();
        return Task.FromResult(results);
    }
}

/// <summary>Reads Cluster resources for diagnostics only; it never participates in Well writes.</summary>
public sealed class WellExternalReferenceValidator : IWellExternalReferenceValidator
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly IHttpClientFactory _clients;
    private readonly IConfiguration _configuration;

    public WellExternalReferenceValidator(IHttpClientFactory clients, IConfiguration configuration)
    {
        _clients = clients;
        _configuration = configuration;
    }

    public async Task<IReadOnlyList<WellExternalReferenceValidation>> ValidateAsync(
        IReadOnlyCollection<WellModel> wells, CancellationToken cancellationToken)
    {
        DateTimeOffset checkedAt = DateTimeOffset.UtcNow;
        Dictionary<Guid, ClusterResolution> clusters = [];
        foreach (Guid clusterId in wells.Where(value => value.ClusterID is Guid id && id != Guid.Empty)
                     .Select(value => value.ClusterID!.Value).Distinct())
            clusters[clusterId] = await ReadClusterAsync(clusterId, cancellationToken);

        return wells.Select(well => Validate(well, checkedAt, clusters)).ToList();
    }

    private async Task<ClusterResolution> ReadClusterAsync(Guid clusterId, CancellationToken cancellationToken)
    {
        string? host = _configuration["ClusterHostURL"];
        if (string.IsNullOrWhiteSpace(host))
            return ClusterResolution.Unavailable("cluster_service_not_configured", "ClusterHostURL is not configured.");
        try
        {
            using HttpClient client = _clients.CreateClient(nameof(WellExternalReferenceValidator));
            client.BaseAddress = new Uri(host.EndsWith('/') ? host : host + "/");
            using HttpResponseMessage response = await client.GetAsync($"Cluster/api/Cluster/{clusterId:D}", cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound) return ClusterResolution.NotFound();
            if (!response.IsSuccessStatusCode)
                return ClusterResolution.Unavailable("cluster_service_error", $"Cluster service returned HTTP {(int)response.StatusCode}.");
            ClusterDto? cluster = await response.Content.ReadFromJsonAsync<ClusterDto>(JsonOptions, cancellationToken);
            if (cluster?.MetaInfo?.ID != clusterId)
                return ClusterResolution.Unavailable("cluster_response_invalid", "Cluster service returned a malformed or mismatched resource.");
            IEnumerable<SlotDto?> slotValues = cluster.Slots?.Values ?? Enumerable.Empty<SlotDto?>();
            HashSet<Guid> slots = slotValues
                .Where(value => value?.ID is Guid id && id != Guid.Empty).Select(value => value!.ID).ToHashSet();
            return ClusterResolution.Found(slots);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException or TaskCanceledException)
        {
            return ClusterResolution.Unavailable("cluster_service_unavailable", "Cluster reference validation is temporarily unavailable.");
        }
    }

    private static WellExternalReferenceValidation Validate(WellModel well, DateTimeOffset checkedAt,
        IReadOnlyDictionary<Guid, ClusterResolution> clusters)
    {
        var result = new WellExternalReferenceValidation
        {
            WellID = well.MetaInfo?.ID ?? Guid.Empty,
            ClusterID = well.ClusterID,
            SlotID = well.SlotID,
            CheckedAtUtc = checkedAt,
            Status = WellExternalReferenceValidationStatus.Valid
        };
        if (well.ClusterID is null)
        {
            if (well.SlotID is not null)
                AddInvalid(result, "SlotID", "cluster_required", "A Slot reference cannot be validated without a ClusterID.");
            return result;
        }
        if (well.ClusterID == Guid.Empty)
        {
            AddInvalid(result, "ClusterID", "empty_uuid", "ClusterID is empty.");
            return result;
        }
        if (!clusters.TryGetValue(well.ClusterID.Value, out ClusterResolution? cluster) || cluster.IsUnavailable)
        {
            result.Status = WellExternalReferenceValidationStatus.Unavailable;
            result.Issues.Add(new WellExternalReferenceIssue
            {
                Property = "ClusterID", Code = cluster?.Code ?? "cluster_service_unavailable",
                Message = cluster?.Message ?? "Cluster reference validation is unavailable."
            });
            return result;
        }
        result.ClusterExists = cluster.Exists;
        if (!cluster.Exists)
        {
            AddInvalid(result, "ClusterID", "cluster_not_found", $"Cluster UUID '{well.ClusterID}' does not exist.");
            return result;
        }
        if (well.SlotID is null) return result;
        if (well.SlotID == Guid.Empty)
        {
            AddInvalid(result, "SlotID", "empty_uuid", "SlotID is empty.");
            return result;
        }
        result.SlotBelongsToCluster = cluster.SlotIDs.Contains(well.SlotID.Value);
        if (result.SlotBelongsToCluster != true)
            AddInvalid(result, "SlotID", "slot_not_in_cluster",
                $"Slot UUID '{well.SlotID}' does not belong to Cluster UUID '{well.ClusterID}'.");
        return result;
    }

    private static void AddInvalid(WellExternalReferenceValidation result, string property, string code, string message)
    {
        result.Status = WellExternalReferenceValidationStatus.Invalid;
        result.Issues.Add(new WellExternalReferenceIssue { Property = property, Code = code, Message = message });
    }

    private sealed class ClusterDto
    {
        public MetaInfoDto? MetaInfo { get; set; }
        public Dictionary<string, SlotDto?>? Slots { get; set; }
    }
    private sealed class MetaInfoDto { public Guid ID { get; set; } }
    private sealed class SlotDto { public Guid ID { get; set; } }
    private sealed record ClusterResolution(bool Exists, bool IsUnavailable, HashSet<Guid> SlotIDs, string? Code, string? Message)
    {
        public static ClusterResolution Found(HashSet<Guid> slots) => new(true, false, slots, null, null);
        public static ClusterResolution NotFound() => new(false, false, [], null, null);
        public static ClusterResolution Unavailable(string code, string message) => new(false, true, [], code, message);
    }
}
