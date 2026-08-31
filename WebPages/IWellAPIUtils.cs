using OSDC.Drilling.Well.ModelShared;

namespace OSDC.Drilling.Well.WebPages;

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

    string HostNameRig { get; }
    string HostBasePathRig { get; }
    HttpClient HttpClientRig { get; }
    Client ClientRig { get; }

    string HostNameTrajectory { get; }
    string HostBasePathTrajectory { get; }
    HttpClient HttpClientTrajectory { get; }
    Client ClientTrajectory { get; }

    string HostNameUnitConversion { get; }
    string HostBasePathUnitConversion { get; }

    string HostNameVerticalDatum { get; }
    string HostBasePathVerticalDatum { get; }
    HttpClient HttpClientVerticalDatum { get; }

    double EarthRadiusWGS84 { get; }
}
