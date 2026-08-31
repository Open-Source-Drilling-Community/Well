using OSDC.Drilling.Well.ModelShared;
using OSDC.DotnetLibraries.Drilling.WebAppUtils;

namespace OSDC.Drilling.Well.WebPages;

public class WellAPIUtils : APIUtils, IWellAPIUtils
{
    public WellAPIUtils(IWellWebPagesConfiguration configuration)
    {
        HostNameWell = Require(configuration.WellHostURL, nameof(configuration.WellHostURL));
        HttpClientWell = SetHttpClient(HostNameWell, HostBasePathWell);
        ClientWell = new Client(HttpClientWell.BaseAddress!.ToString(), HttpClientWell);

        HostNameField = Require(configuration.FieldHostURL, nameof(configuration.FieldHostURL));
        HttpClientField = SetHttpClient(HostNameField, HostBasePathField);
        ClientField = new Client(HttpClientField.BaseAddress!.ToString(), HttpClientField);

        HostNameCluster = Require(configuration.ClusterHostURL, nameof(configuration.ClusterHostURL));
        HttpClientCluster = SetHttpClient(HostNameCluster, HostBasePathCluster);
        ClientCluster = new Client(HttpClientCluster.BaseAddress!.ToString(), HttpClientCluster);

        HostNameRig = Require(configuration.RigHostURL, nameof(configuration.RigHostURL));
        HttpClientRig = SetHttpClient(HostNameRig, HostBasePathRig);
        ClientRig = new Client(HttpClientRig.BaseAddress!.ToString(), HttpClientRig);

        HostNameTrajectory = Require(configuration.TrajectoryHostURL, nameof(configuration.TrajectoryHostURL));
        HttpClientTrajectory = SetHttpClient(HostNameTrajectory, HostBasePathTrajectory);
        ClientTrajectory = new Client(HttpClientTrajectory.BaseAddress!.ToString(), HttpClientTrajectory);

        HostNameUnitConversion = Require(configuration.UnitConversionHostURL, nameof(configuration.UnitConversionHostURL));

        HostNameVerticalDatum = Require(configuration.EarthVerticalDatumHostURL, nameof(configuration.EarthVerticalDatumHostURL));
        HttpClientVerticalDatum = SetHttpClient(HostNameVerticalDatum, HostBasePathVerticalDatum);
    }

    private static string Require(string? value, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Configuration value '{propertyName}' must be assigned before WebPages is used.");
        }

        return value;
    }

    public string HostNameWell { get; }
    public string HostBasePathWell { get; } = "Well/api/";
    public HttpClient HttpClientWell { get; }
    public Client ClientWell { get; }

    public string HostNameField { get; }
    public string HostBasePathField { get; } = "Field/api/";
    public HttpClient HttpClientField { get; }
    public Client ClientField { get; }

    public string HostNameCluster { get; }
    public string HostBasePathCluster { get; } = "Cluster/api/";
    public HttpClient HttpClientCluster { get; }
    public Client ClientCluster { get; }

    public string HostNameRig { get; }
    public string HostBasePathRig { get; } = "Rig/api/";
    public HttpClient HttpClientRig { get; }
    public Client ClientRig { get; }

    public string HostNameTrajectory { get; }
    public string HostBasePathTrajectory { get; } = "Trajectory/api/";
    public HttpClient HttpClientTrajectory { get; }
    public Client ClientTrajectory { get; }

    public string HostNameUnitConversion { get; }
    public string HostBasePathUnitConversion { get; } = "UnitConversion/api/";

    public string HostNameVerticalDatum { get; }
    public string HostBasePathVerticalDatum { get; } = "VerticalDatum/api/";
    public HttpClient HttpClientVerticalDatum { get; }

    public double EarthRadiusWGS84 { get; } = 6378137.0;
}
