using NORCE.Drilling.Well.WebPages;

namespace NORCE.Drilling.Well.WebApp;

public class WebPagesHostConfiguration : IWellWebPagesConfiguration
{
    public string WellHostURL { get; set; } = string.Empty;
    public string ClusterHostURL { get; set; } = string.Empty;
    public string FieldHostURL { get; set; } = string.Empty;
    public string TrajectoryHostURL { get; set; } = string.Empty;
    public string UnitConversionHostURL { get; set; } = string.Empty;
}
