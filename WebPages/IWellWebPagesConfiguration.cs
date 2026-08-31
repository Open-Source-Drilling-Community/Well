using OSDC.DotnetLibraries.Drilling.WebAppUtils;

namespace NORCE.Drilling.Well.WebPages;

public interface IWellWebPagesConfiguration :
    IWellHostURL,
    IClusterHostURL,
    IFieldHostURL,
    IRigHostURL,
    ITrajectoryHostURL,
    IUnitConversionHostURL
{
    string? VerticalDatumHostURL { get; set; }
}
