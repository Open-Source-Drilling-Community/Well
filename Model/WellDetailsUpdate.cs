using System.Text.Json.Serialization;

namespace OSDC.Drilling.Well.Model;

/// <summary>Complete replacement of the small, independently mutable Well details sub-resource.</summary>
public sealed class WellDetailsUpdate
{
    [JsonRequired]
    public string? Name { get; set; }

    [JsonRequired]
    public string? Description { get; set; }
}
