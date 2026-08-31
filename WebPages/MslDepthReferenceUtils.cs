using System.Net.Http.Json;
using System.Text.Json;
using ModelShared = NORCE.Drilling.Well.ModelShared;

namespace NORCE.Drilling.Well.WebPages;

public static class MslDepthReferenceUtils
{
    public static Task<double?> ResolveMeanSeaLevelDepthReferenceAsync(IWellAPIUtils api, ModelShared.Well? well, IEnumerable<ModelShared.Cluster>? clusters)
    {
        ModelShared.Slot? slot = ResolveSlot(well, clusters);
        return CalculateMeanSeaLevelDepthReferenceAsync(
            api.HttpClientVerticalDatum,
            api.HostNameVerticalDatum,
            api.HostBasePathVerticalDatum,
            slot?.Latitude?.GaussianValue?.Mean,
            slot?.Longitude?.GaussianValue?.Mean);
    }

    private static ModelShared.Slot? ResolveSlot(ModelShared.Well? well, IEnumerable<ModelShared.Cluster>? clusters)
    {
        if (well?.SlotID is not Guid slotId || clusters == null)
        {
            return null;
        }

        ModelShared.Cluster? cluster = null;
        if (well.ClusterID is Guid clusterId)
        {
            cluster = clusters.FirstOrDefault(item => item?.MetaInfo?.ID == clusterId);
        }

        cluster ??= clusters.FirstOrDefault(item => item?.Slots?.Values.Any(slot => slot?.ID == slotId) == true);
        return cluster?.Slots?.Values.FirstOrDefault(slot => slot?.ID == slotId);
    }

    private static async Task<double?> CalculateMeanSeaLevelDepthReferenceAsync(HttpClient client, string hostName, string hostBasePath, double? latitude, double? longitude)
    {
        if (latitude == null || longitude == null)
        {
            return null;
        }

        Guid orderId = Guid.NewGuid();
        object order = new
        {
            MetaInfo = new { ID = orderId, HttpHostName = hostName, HttpHostBasePath = hostBasePath, HttpEndPoint = "VerticalDatumOrder/" },
            Name = $"MSL reference {orderId}",
            Description = "Temporary MSL-to-WGS84 conversion.",
            CreationDate = DateTimeOffset.UtcNow,
            LastModificationDate = DateTimeOffset.UtcNow,
            VerticalDatum = new
            {
                MetaInfo = new { ID = Guid.NewGuid(), HttpHostName = hostName, HttpHostBasePath = hostBasePath, HttpEndPoint = "VerticalDatum/" },
                Name = $"MSL reference {orderId}",
                Description = "Temporary MSL-to-WGS84 conversion.",
                CreationDate = DateTimeOffset.UtcNow,
                LastModificationDate = DateTimeOffset.UtcNow,
                DatumSet = new[] { new { Latitude = latitude.Value, Longitude = longitude.Value, GenericVerticalDatum = 0 } },
                ConversionFrom = "FromMeanSeaLevel",
                Type = "Raw"
            }
        };

        try
        {
            using HttpResponseMessage postResponse = await client.PostAsJsonAsync("VerticalDatumOrder", order);
            postResponse.EnsureSuccessStatusCode();

            using JsonDocument document = await client.GetFromJsonAsync<JsonDocument>($"VerticalDatumOrder/{orderId}") ?? throw new InvalidOperationException("VerticalDatumOrder response was empty.");
            JsonElement datumSet = document.RootElement.GetProperty("VerticalDatum").GetProperty("DatumSet");
            if (datumSet.GetArrayLength() == 0 ||
                !datumSet[0].TryGetProperty("VerticalDatumWGS64", out JsonElement valueElement) ||
                valueElement.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            return -valueElement.GetDouble();
        }
        finally
        {
            try
            {
                await client.DeleteAsync($"VerticalDatumOrder/{orderId}");
            }
            catch
            {
                // Best-effort cleanup of a temporary calculation order.
            }
        }
    }
}
