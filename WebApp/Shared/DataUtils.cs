public static class DataUtils
{
    // default values
    public const double DEFAULT_VALUE = 999.25;
    public static string DEFAULT_NAME_Well = "Default Well Name";
    public static string DEFAULT_DESCR_Well = "Default Well Description";
    public static string DEFAULT_NAME_MyBaseData = "Default MyBaseData Name";
    public static string DEFAULT_DESCR_MyBaseData = "Default MyBaseData Description";

    // unit management
    public static class UnitAndReferenceParameters
    {
        public static string? UnitSystemName { get; set; } = "Metric";
        public static string? DepthReferenceName { get; set; }
        public static string? PositionReferenceName { get; set; }
        public static string? AzimuthReferenceName { get; set; }
        public static string? PressureReferenceName { get; set; }
        public static string? DateReferenceName { get; set; }
    }

    public static void UpdateUnitSystemName(string val)
    {
        UnitAndReferenceParameters.UnitSystemName = (string)val;
    }

    public static readonly string WellNameLabel = "Well Name";
    public static readonly string WellDescrLabel = "Well Description";

    public static readonly string DepthReferencesXValuesTitle = "Departure";
    public static readonly string DepthReferencesXValuesQty = "LengthStandard";
    public static readonly string DepthReferencesYValuesTitle = "Depth";
    public static readonly string DepthReferencesYValuesQty = "DepthDrilling";

}