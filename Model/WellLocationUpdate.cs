using System;
using System.Text.Json.Serialization;

namespace OSDC.Drilling.Well.Model;

/// <summary>Complete replacement of a Well's external Cluster/Slot placement sub-resource.</summary>
public sealed class WellLocationUpdate
{
    [JsonRequired]
    public Guid? ClusterID { get; set; }

    [JsonRequired]
    public Guid? SlotID { get; set; }

    [JsonRequired]
    public bool IsSingleWell { get; set; }
}
