using System;
using System.Collections.Generic;

namespace OSDC.Drilling.Well.Model;

public enum WellBatchExportScope
{
    Unspecified = 0,
    All = 1,
    Selected = 2
}

public sealed class WellBatchExportRequest
{
    public WellBatchExportScope Scope { get; set; }
    public List<Guid>? WellIDs { get; set; }
}

/// <summary>A portable, versioned backup of Wells and their referenced local catalogs.</summary>
public sealed class WellBatchExportDocument
{
    public const string CurrentFormatIdentifier = "OSDC.Drilling.Well.BatchExport";
    public const int CurrentSchemaVersion = 1;

    public string FormatIdentifier { get; set; } = CurrentFormatIdentifier;
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public DateTimeOffset ExportedAtUtc { get; set; }
    public WellBatchCatalogDependencies CatalogDependencies { get; set; } = new();
    public List<Well> Wells { get; set; } = [];
}

public sealed class WellBatchCatalogDependencies
{
    public List<WellIdentity> Identities { get; set; } = [];
    public List<WellFeatureCategory> FeatureCategories { get; set; } = [];
}

public sealed class WellBatchErrorEnvelope
{
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public List<WellBatchError> Errors { get; set; } = [];
}

public sealed class WellBatchError
{
    public int? PositionIndex { get; set; }
    public string Property { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public enum WellBatchRestoreConflictPolicy
{
    Unspecified = 0,
    FailIfExists = 1,
    ReplaceExisting = 2
}

public enum WellBatchCatalogRestorePolicy
{
    Unspecified = 0,
    MapExisting = 1,
    MapOrCreateMissing = 2
}

public sealed class WellBatchRestoreRequest
{
    public WellBatchRestoreConflictPolicy ConflictPolicy { get; set; }
    public WellBatchCatalogRestorePolicy CatalogPolicy { get; set; }
    public WellBatchExportDocument? Document { get; set; }
}

public sealed class WellBatchRestoreResponse
{
    public DateTimeOffset RestoredAtUtc { get; set; }
    public int CreatedCount { get; set; }
    public int ReplacedCount { get; set; }
    public int CreatedCatalogDefinitionCount { get; set; }
    public int CreatedCatalogOptionCount { get; set; }
    public List<WellBatchCatalogMapping> CatalogMappings { get; set; } = [];
    public List<Guid> WellIDs { get; set; } = [];
}

public sealed class WellBatchCatalogMapping
{
    public string Catalog { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid SourceID { get; set; }
    public Guid LocalID { get; set; }
    public string Resolution { get; set; } = string.Empty;
}
