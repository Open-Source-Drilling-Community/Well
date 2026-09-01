using System.Collections.Generic;

namespace OSDC.Drilling.Well.Model;

/// <summary>A stable page of Wells matching server-side filters.</summary>
public sealed class WellSearchResult
{
    public List<Well> Items { get; set; } = [];

    public int Total { get; set; }

    public int Offset { get; set; }

    public int Limit { get; set; }
}
