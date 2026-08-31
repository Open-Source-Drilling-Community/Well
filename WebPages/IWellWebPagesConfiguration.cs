using OSDC.DotnetLibraries.Drilling.WebAppUtils;

namespace OSDC.Drilling.Well.WebPages;

public interface IWellWebPagesConfiguration :
    IWellHostURL,
    IClusterHostURL,
    IFieldHostURL,
    IRigHostURL,
    ITrajectoryHostURL,
    IUnitConversionHostURL
{
    string? EarthVerticalDatumHostURL { get; set; }
}
