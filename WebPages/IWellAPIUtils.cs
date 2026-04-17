using NORCE.Drilling.Well.ModelShared;

namespace NORCE.Drilling.Well.WebPages;

public interface IWellAPIUtils
{
    string HostNameWell { get; }
    string HostBasePathWell { get; }
    HttpClient HttpClientWell { get; }
    Client ClientWell { get; }

    string HostNameField { get; }
    string HostBasePathField { get; }
    HttpClient HttpClientField { get; }
    Client ClientField { get; }

    string HostNameCluster { get; }
    string HostBasePathCluster { get; }
    HttpClient HttpClientCluster { get; }
    Client ClientCluster { get; }

    string HostNameUnitConversion { get; }
    string HostBasePathUnitConversion { get; }

    double EarthRadiusWGS84 { get; }
}
