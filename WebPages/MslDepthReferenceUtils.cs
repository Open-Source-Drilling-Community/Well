using ModelShared = OSDC.Drilling.Well.ModelShared;

namespace OSDC.Drilling.Well.WebPages;

public static class MslDepthReferenceUtils
{
    public static Task<double?> ResolveMeanSeaLevelDepthReferenceAsync(IWellAPIUtils api, ModelShared.Well? well, IEnumerable<ModelShared.Cluster>? clusters)
    {
        ModelShared.Slot? slot = ResolveSlot(well, clusters);
        return CalculateMeanSeaLevelDepthReferenceAsync(
            api,
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

    public static async Task<double?> CalculateMeanSeaLevelDepthReferenceAsync(
        IWellAPIUtils api,
        double? latitude,
        double? longitude)
    {
        if (latitude == null || longitude == null)
        {
            return null;
        }

        ModelShared.MeanSeaLevelToWgs84Request request = new()
        {
            Positions =
            [
                new ModelShared.EarthVerticalDatumPosition
                {
                    Latitude = latitude.Value,
                    Longitude = longitude.Value,
                    MeanSeaLevelDepth = 0
                }
            ]
        };
        ModelShared.MeanSeaLevelToWgs84Response response =
            await api.ClientEarthVerticalDatum.ConvertMeanSeaLevelToWgs84Async(request);
        return response.Samples?.FirstOrDefault()?.Wgs84EllipsoidalDepth;
    }
}
