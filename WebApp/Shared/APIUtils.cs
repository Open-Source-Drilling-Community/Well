public static class APIUtils
{
    // API parameters
    public static readonly string HostNameWell = NORCE.Drilling.Well.WebApp.Configuration.WellHostURL!;
    public static readonly string HostBasePathWell = "Well/api/";
    public static readonly HttpClient HttpClientWell = APIUtils.SetHttpClient(HostNameWell, HostBasePathWell);
    public static readonly NORCE.Drilling.Well.ModelShared.Client ClientWell = new NORCE.Drilling.Well.ModelShared.Client(APIUtils.HttpClientWell.BaseAddress!.ToString(), APIUtils.HttpClientWell);

    public static readonly string HostNameField = NORCE.Drilling.Well.WebApp.Configuration.FieldHostURL!;
    public static readonly string HostBasePathField = "Field/api/";
    public static readonly HttpClient HttpClientField = APIUtils.SetHttpClient(HostNameField, HostBasePathField);
    public static readonly NORCE.Drilling.Well.ModelShared.Client ClientField = new NORCE.Drilling.Well.ModelShared.Client(APIUtils.HttpClientField.BaseAddress!.ToString(), APIUtils.HttpClientField);

    public static readonly string HostNameCluster = NORCE.Drilling.Well.WebApp.Configuration.ClusterHostURL!;
    public static readonly string HostBasePathCluster = "Cluster/api/";
    public static readonly HttpClient HttpClientCluster = APIUtils.SetHttpClient(HostNameCluster, HostBasePathCluster);
    public static readonly NORCE.Drilling.Well.ModelShared.Client ClientCluster = new NORCE.Drilling.Well.ModelShared.Client(APIUtils.HttpClientCluster.BaseAddress!.ToString(), APIUtils.HttpClientCluster);

    public static readonly string HostNameUnitConversion = NORCE.Drilling.Well.WebApp.Configuration.UnitConversionHostURL!;
    public static readonly string HostBasePathUnitConversion = "UnitConversion/api/";

    public static readonly double EarthRadiusWGS84 = 6378137.0; // in meters

    // API utility methods
    public static HttpClient SetHttpClient(string host, string microServiceUri)
    {
        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => { return true; }; // temporary workaround for testing purposes: bypass certificate validation (not recommended for production environments due to security risks)
        HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri(host + microServiceUri)
        };
        httpClient.DefaultRequestHeaders.Accept.Clear();
        httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        return httpClient;
    }
}