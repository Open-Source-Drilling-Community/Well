using OSDC.DotnetLibraries.Drilling.SemanticCatalogue;
using System.Text.Json.Serialization;

namespace OSDC.Drilling.Well.Model;

/// <summary>Complete replacement of the small, independently mutable Well details sub-resource.</summary>
public sealed class WellDetailsUpdate
{
    [JsonRequired]
    [Semantic(Concepts.ResourceName)]
    public string? Name { get; set; }

    [JsonRequired]
    [Semantic(Concepts.ResourceDescription)]
    public string? Description { get; set; }
}
