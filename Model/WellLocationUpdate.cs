using OSDC.DotnetLibraries.Drilling.SemanticCatalogue;
using System;
using System.Text.Json.Serialization;

namespace OSDC.Drilling.Well.Model;

/// <summary>Complete replacement of a Well's external Cluster/Slot placement sub-resource.</summary>
public sealed class WellLocationUpdate
{
    [JsonRequired]
    [Semantic(Concepts.ResourceIdentifier)]
    public Guid? ClusterID { get; set; }

    [JsonRequired]
    [Semantic(Concepts.ResourceIdentifier)]
    public Guid? SlotID { get; set; }

    [JsonRequired]
    [Semantic(Concepts.SingleWellClusterFlag)]
    public bool IsSingleWell { get; set; }
}
