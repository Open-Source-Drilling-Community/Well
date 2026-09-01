using System;
using System.Collections.Generic;

namespace OSDC.Drilling.Well.Model;

/// <summary>
/// Stable error envelope for Well and locally owned catalog mutations.
/// </summary>
public sealed class WellMutationErrorEnvelope
{
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public List<WellMutationError> Errors { get; set; } = [];
}

/// <summary>
/// Identifies an invalid reference, an active dependent reference, or a stale
/// optimistic-concurrency token.
/// </summary>
public sealed class WellMutationError
{
    public string Property { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public List<Guid> ReferencingWellIDs { get; set; } = [];
}
