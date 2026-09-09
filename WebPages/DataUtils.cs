using OSDC.Drilling.Well.ModelShared;
using OSDC.UnitConversion.DrillingRazorMudComponents;

namespace OSDC.Drilling.Well.WebPages;

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
        public static string? PositionReferenceName { get; set; } = "WGS84";
        public static string? AzimuthReferenceName { get; set; }
        public static string? PressureReferenceName { get; set; }
        public static string? DateReferenceName { get; set; }
        public static GroundMudLineDepthReferenceSource GroundMudLineDepthReferenceSource { get; set; } = new();
        public static SeaWaterLevelDepthReferenceSource SeaWaterLevelDepthReferenceSource { get; set; } = new();
        public static RotaryTableDepthReferenceSource RotaryTableDepthReferenceSource { get; set; } = new();
        public static MeanSeaLevelDepthReferenceSource MeanSeaLevelDepthReferenceSource { get; set; } = new();
        public static WellHeadPositionReferenceSource WellHeadPositionReferenceSource { get; set; } = new();
        public static ClusterPositionReferenceSource ClusterPositionReferenceSource { get; set; } = new();
        public static FieldPositionReferenceSource FieldPositionReferenceSource { get; set; } = new();
        public static CartographicGridPositionReferenceSource CartographicGridPositionReferenceSource { get; set; } = new();
    }

    public static void ApplyWellReferenceValues(OSDC.Drilling.Well.ModelShared.Well? well, List<Cluster> clusters,
        List<RigReadResponse>? rigs = null, List<Field>? fields = null)
    {
        UnitAndReferenceParameters.GroundMudLineDepthReferenceSource.GroundMudLineDepthReference = 0;
        UnitAndReferenceParameters.SeaWaterLevelDepthReferenceSource.SeaWaterLevelDepthReference = 0;
        UnitAndReferenceParameters.RotaryTableDepthReferenceSource.RotaryTableDepthReference = null;
        UnitAndReferenceParameters.MeanSeaLevelDepthReferenceSource.MeanSeaLevelDepthReference = null;
        UnitAndReferenceParameters.WellHeadPositionReferenceSource.WellHeadNorthPositionReference = null;
        UnitAndReferenceParameters.WellHeadPositionReferenceSource.WellHeadEastPositionReference = null;
        UnitAndReferenceParameters.ClusterPositionReferenceSource.ClusterNorthPositionReference = null;
        UnitAndReferenceParameters.ClusterPositionReferenceSource.ClusterEastPositionReference = null;
        UnitAndReferenceParameters.FieldPositionReferenceSource.FieldNorthPositionReference = null;
        UnitAndReferenceParameters.FieldPositionReferenceSource.FieldEastPositionReference = null;
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
            if (cluster?.IsFixedPlatform == true && cluster.RigID is Guid rigId && rigId != Guid.Empty && rigs != null)
            {
                RigReadResponse? rig = rigs.FirstOrDefault(item => item?.MetaInfo?.ID == rigId);
                ApplyRotaryTableDepthWGS84(rig?.FixedPlatformProperties?.DrillFloorDepth?.GaussianValue?.Mean);
            }

            UnitAndReferenceParameters.ClusterPositionReferenceSource.ClusterNorthPositionReference = -cluster?.ReferencePoint?.RiemannianNorth;
            UnitAndReferenceParameters.ClusterPositionReferenceSource.ClusterEastPositionReference = -cluster?.ReferencePoint?.RiemannianEast;

            Field? field = fields?.FirstOrDefault(item => item?.MetaInfo?.ID == cluster?.FieldID);
            UnitAndReferenceParameters.FieldPositionReferenceSource.FieldNorthPositionReference = -field?.ReferencePoint?.RiemannianNorth;
            UnitAndReferenceParameters.FieldPositionReferenceSource.FieldEastPositionReference = -field?.ReferencePoint?.RiemannianEast;

            Slot? slot = cluster?.Slots?.Values.FirstOrDefault(item => item.ID == well.SlotID);
            SetWellHeadPositionReference(slot);
        }

        if (UnitAndReferenceParameters.RotaryTableDepthReferenceSource.RotaryTableDepthReference == null &&
            string.Equals(UnitAndReferenceParameters.DepthReferenceName, "Rotary table", StringComparison.Ordinal))
        {
            UnitAndReferenceParameters.DepthReferenceName = "WGS84";
        }
    }

    private static void SetWellHeadPositionReference(Slot? slot)
    {
        if (slot?.Latitude?.GaussianValue?.Mean is not double latitude ||
            slot.Longitude?.GaussianValue?.Mean is not double longitude)
        {
            return;
        }

        var wellHead = new OSDC.DotnetLibraries.General.Math.Point3DGlobalCoordinates
        {
            Latitude = latitude,
            Longitude = longitude
        };
        UnitAndReferenceParameters.WellHeadPositionReferenceSource.WellHeadNorthPositionReference = -wellHead.RiemannianNorth;
        UnitAndReferenceParameters.WellHeadPositionReferenceSource.WellHeadEastPositionReference = -wellHead.RiemannianEast;
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

    public static void ApplyRotaryTableDepthWGS84(double? val)
    {
        if (val != null)
        {
            UnitAndReferenceParameters.RotaryTableDepthReferenceSource.RotaryTableDepthReference = -val;
        }
    }

    public static void UpdateUnitSystemName(string value) => UnitAndReferenceParameters.UnitSystemName = value;
    public static void UpdateDepthReferenceName(string value) => UnitAndReferenceParameters.DepthReferenceName = value;
    public static void UpdatePositionReferenceName(string value) => UnitAndReferenceParameters.PositionReferenceName = value;

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

    public class RotaryTableDepthReferenceSource : IRotaryTableDepthReferenceSource
    {
        public double? RotaryTableDepthReference { get; set; }
    }

    public class MeanSeaLevelDepthReferenceSource : IMeanSeaLevelDepthReferenceSource
    {
        public double? MeanSeaLevelDepthReference { get; set; }
    }

    public class WellHeadPositionReferenceSource : IWellHeadPositionReferenceSource
    {
        public double? WellHeadNorthPositionReference { get; set; }
        public double? WellHeadEastPositionReference { get; set; }
    }

    public class ClusterPositionReferenceSource : IClusterPositionReferenceSource
    {
        public double? ClusterNorthPositionReference { get; set; }
        public double? ClusterEastPositionReference { get; set; }
    }

    public class FieldPositionReferenceSource : IFieldPositionReferenceSource
    {
        public double? FieldNorthPositionReference { get; set; }
        public double? FieldEastPositionReference { get; set; }
    }

    public class CartographicGridPositionReferenceSource : ICartographicGridPositionReferenceSource
    {
        public double? CartographicGridNorthPositionReference { get; set; }
        public double? CartographicGridEastPositionReference { get; set; }
    }
}
