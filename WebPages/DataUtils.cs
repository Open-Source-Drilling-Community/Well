using NORCE.Drilling.Well.ModelShared;
using OSDC.UnitConversion.DrillingRazorMudComponents;

namespace NORCE.Drilling.Well.WebPages;

public static class DataUtils
{
    public const double DEFAULT_VALUE = 999.25;
    public static string DEFAULT_NAME_Well = "Default Well Name";
    public static string DEFAULT_DESCR_Well = "Default Well Description";
    public static string DEFAULT_NAME_MyBaseData = "Default MyBaseData Name";
    public static string DEFAULT_DESCR_MyBaseData = "Default MyBaseData Description";

    public static class UnitAndReferenceParameters
    {
        public static string? UnitSystemName { get; set; } = "Metric";
        public static string? DepthReferenceName { get; set; } = "WGS84";
        public static string? PositionReferenceName { get; set; }
        public static string? AzimuthReferenceName { get; set; }
        public static string? PressureReferenceName { get; set; }
        public static string? DateReferenceName { get; set; }
        public static GroundMudLineDepthReferenceSource GroundMudLineDepthReferenceSource { get; set; } = new();
        public static SeaWaterLevelDepthReferenceSource SeaWaterLevelDepthReferenceSource { get; set; } = new();
    }

    public static void ApplyWellReferenceValues(NORCE.Drilling.Well.ModelShared.Well? well, List<Cluster> clusters)
    {
        UnitAndReferenceParameters.GroundMudLineDepthReferenceSource.GroundMudLineDepthReference = 0;
        UnitAndReferenceParameters.SeaWaterLevelDepthReferenceSource.SeaWaterLevelDepthReference = 0;
        if (well != null && well.ClusterID != null)
        {
            Cluster? cluster = null;
            foreach (var c in clusters)
            {
                if (c?.MetaInfo != null && c.MetaInfo.ID == well.ClusterID)
                {
                    cluster = c;
                    break;
                }
            }

            if (cluster?.GroundMudLineDepth?.GaussianValue?.Mean != null)
            {
                ApplyGroundMudLineDepthWGS84(cluster.GroundMudLineDepth.GaussianValue.Mean);
            }
            if (cluster?.TopWaterDepth?.GaussianValue?.Mean != null)
            {
                ApplyTopWaterDepthWGS84(cluster.TopWaterDepth.GaussianValue.Mean);
            }
        }
    }

    public static void ApplyGroundMudLineDepthWGS84(double? val)
    {
        if (val != null)
        {
            UnitAndReferenceParameters.GroundMudLineDepthReferenceSource.GroundMudLineDepthReference = -val;
        }
    }

    public static void ApplyTopWaterDepthWGS84(double? val)
    {
        if (val != null)
        {
            UnitAndReferenceParameters.SeaWaterLevelDepthReferenceSource.SeaWaterLevelDepthReference = -val;
        }
    }

    public static void UpdateUnitSystemName(string value) => UnitAndReferenceParameters.UnitSystemName = value;
    public static void UpdateDepthReferenceName(string value) => UnitAndReferenceParameters.DepthReferenceName = value;

    public static readonly string WellNameLabel = "Well Name";
    public static readonly string WellDescrLabel = "Well Description";

    public static readonly string DepthReferencesXValuesTitle = "Departure";
    public static readonly string DepthReferencesXValuesQty = "LengthStandard";
    public static readonly string DepthReferencesYValuesTitle = "Depth";
    public static readonly string DepthReferencesYValuesQty = "DepthDrilling";

    public class GroundMudLineDepthReferenceSource : IGroundMudLineDepthReferenceSource
    {
        public double? GroundMudLineDepthReference { get; set; }
    }

    public class SeaWaterLevelDepthReferenceSource : ISeaWaterLevelDepthReferenceSource
    {
        public double? SeaWaterLevelDepthReference { get; set; }
    }
}
