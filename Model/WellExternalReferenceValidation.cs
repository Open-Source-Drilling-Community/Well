using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OSDC.Drilling.Well.Model;

public enum WellExternalReferenceValidationStatus
{
    Valid,
    Invalid,
    Unavailable
}

public sealed class WellExternalReferenceIssue
{
    public string Property { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class WellExternalReferenceValidation
{
    public Guid WellID { get; set; }
    public Guid? ClusterID { get; set; }
    public Guid? SlotID { get; set; }
    public bool? ClusterExists { get; set; }
    public bool? SlotBelongsToCluster { get; set; }
    public WellExternalReferenceValidationStatus Status { get; set; }
    public DateTimeOffset CheckedAtUtc { get; set; }
    public List<WellExternalReferenceIssue> Issues { get; set; } = [];
}

public enum WellExternalReferenceAuditScope
{
    All,
    Selected
}

public sealed class WellExternalReferenceAuditRequest
{
    [JsonRequired]
    public WellExternalReferenceAuditScope Scope { get; set; }

    public List<Guid>? WellIDs { get; set; }

    public int Offset { get; set; }

    public int Limit { get; set; } = 100;
}

public sealed class WellExternalReferenceAuditResult
{
    public DateTimeOffset CheckedAtUtc { get; set; }
    public int Total { get; set; }
    public int Offset { get; set; }
    public int Limit { get; set; }
    public int ValidCount { get; set; }
    public int InvalidCount { get; set; }
    public int UnavailableCount { get; set; }
    public List<WellExternalReferenceValidation> Items { get; set; } = [];
}
