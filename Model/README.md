# OSDC.Drilling.Well.Model

The Model project is the authoritative serializable contract used by the Well service. It targets .NET 8 with nullable reference types enabled.

## Domain contracts

- `Well`: metadata, name, description, timestamps, optional `ClusterID` and `SlotID`, `IsSingleWell`, identity assignments, and feature assignments.
- `WellIdentity`: user-managed symbolic identity definition.
- `WellIdentityAssignment`: assignment UUID, referenced identity UUID, and Well-specific value.
- `WellSearchResult`: a bounded page of complete Wells with `Total`, `Offset`, and `Limit` metadata.
- `WellFeatureCategory`: user-managed category with exclusivity and validity-period semantics.
- `WellFeatureOption`: stable option UUID and name within a feature category.
- `WellFeatureAssignment`: referenced category and option plus optional validity dates.
- `WellMutationErrorEnvelope`: structured mutation and catalog-conflict errors.

The service supplies default identity and feature definitions for initial installations, while the definitions remain editable through the catalog APIs and UI.

## Backup and restore contracts

`WellBatchExport.cs` defines the portable format and policies:

- `WellBatchExportRequest`: export `All` Wells or a non-empty ordered `Selected` list.
- `WellBatchExportDocument`: format identifier `OSDC.Drilling.Well.BatchExport`, schema version 1, UTC export time, referenced catalogs, and complete Wells.
- `WellBatchRestoreRequest`: conflict and catalog-resolution policies plus the document.
- Conflict policies: `FailIfExists` and `ReplaceExisting`.
- Catalog policies: `MapExisting` and `MapOrCreateMissing`.
- `WellBatchRestoreResponse`: created/replaced counts and source-to-local catalog UUID mappings.

Cluster and Slot UUIDs are external references and are not copied as resources into a Well backup.

## Usage statistics

`UsageStatisticsWell` records daily counters for the Well endpoints and periodically persists them to `../home/history.json`. Controllers call the appropriate increment methods; consumers should treat the singleton as service infrastructure rather than domain state.

## Example

```csharp
using OSDC.DotnetLibraries.General.DataManagement;
using OSDC.Drilling.Well.Model;

var identityId = Guid.NewGuid();
var categoryId = Guid.NewGuid();
var optionId = Guid.NewGuid();

var well = new Well
{
    MetaInfo = new MetaInfo { ID = Guid.NewGuid() },
    Name = "Example Well",
    ClusterID = Guid.NewGuid(),
    SlotID = Guid.NewGuid(),
    WellIdentityAssignments =
    [
        new WellIdentityAssignment
        {
            ID = Guid.NewGuid(),
            IdentityID = identityId,
            Value = "A-01"
        }
    ],
    WellFeatureAssignments =
    [
        new WellFeatureAssignment
        {
            ID = Guid.NewGuid(),
            FeatureCategoryID = categoryId,
            FeatureOptionID = optionId
        }
    ]
};
```

Catalog references are validated by the Service when a Well is created, updated, exported, or restored.

## Dependencies and build

The project depends on the OSDC drilling-property, common, data-management, and statistics libraries. Exact versions are in `Model.csproj`.

```powershell
dotnet build Model\Model.csproj
dotnet test ModelTest\ModelTest.csproj
```

DocFX configuration is available in `Model/docfx.json`; `Model/api` and `Model/articles` contain its source files.
