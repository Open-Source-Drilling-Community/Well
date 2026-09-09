# OSDC.Drilling.Well.WebPages

This release targets MudBlazor 9.9.0 and the matching OSDC shared web component packages.

`OSDC.Drilling.Well.WebPages` is a .NET 8 Razor class library containing the reusable UI for the Well microservice. The NuGet package ID is `OSDC.Drilling.Well.WebPages`; its current project version is defined in `WebPages.csproj`.

## Pages

| Component | Route | Purpose |
| --- | --- | --- |
| `WellMain` | `/Well` | Browse, select, create, edit, and delete Wells. |
| `WellEdit` | Rendered by Well workflows | Edit core Well data, identity values, and feature assignments. |
| `WellBackupRestore` | `/WellBackupRestore` | Export versioned JSON and validate/restore it with explicit policies. |
| `WellIdentities` | `/WellIdentities` | Add, edit, and remove Identity definitions with reference protection. |
| `WellFeatures` | `/WellFeatures` | Add, edit, and remove Feature Categories and options. |
| `WellTrajectories` | `/WellTrajectories` | Display Well trajectory data. |
| `WellSurveyRuns` | `/WellSurveyRuns` | Display Well survey-run data. |
| `StatisticsWell` | `/StatisticsWell` | Refreshable per-endpoint request totals, today's counts, and last-use times. |

The package also contains `ScatterPlot`, `Scatter3DPlot`, `MslDepthReferenceUtils`, API utilities, and `wwwroot/wellBatchBackup.js` for browser-side JSON download.

The Field, Cluster, and Well selectors on both trajectory and survey-run pages show every applicable item when empty and support case-insensitive substring filtering while typing.
Their shared unit/reference control also converts plotted North/East coordinates between WGS84, the selected Field reference point, the selected Cluster reference point, the selected Well-head slot, and the owning Field's cartographic projection. WGS84 metres remain the canonical wire values.

## Host requirements

A consuming Blazor host must:

1. Reference the package or project.
2. Register MudBlazor services and any other host-wide Blazor services.
3. Provide all values required by `IWellWebPagesConfiguration`.
4. Register the configuration and `IWellAPIUtils`.
5. Add the WebPages assembly to the Blazor router's `AdditionalAssemblies`.
6. Serve static web assets so the backup download module is available under `_content/OSDC.Drilling.Well.WebPages`.

Example configuration:

```csharp
using OSDC.Drilling.Well.WebPages;

var configuration = new WebPagesHostConfiguration
{
    WellHostURL = builder.Configuration["WellHostURL"] ?? string.Empty,
    ClusterHostURL = builder.Configuration["ClusterHostURL"] ?? string.Empty,
    FieldHostURL = builder.Configuration["FieldHostURL"] ?? string.Empty,
    RigHostURL = builder.Configuration["RigHostURL"] ?? string.Empty,
    TrajectoryHostURL = builder.Configuration["TrajectoryHostURL"] ?? string.Empty,
    EarthVerticalDatumHostURL = builder.Configuration["EarthVerticalDatumHostURL"] ?? string.Empty,
    UnitConversionHostURL = builder.Configuration["UnitConversionHostURL"] ?? string.Empty
};

builder.Services.AddSingleton<IWellWebPagesConfiguration>(configuration);
builder.Services.AddSingleton<IWellAPIUtils, WellAPIUtils>();
```

`WebPagesHostConfiguration` above is a host-defined class implementing `IWellWebPagesConfiguration`; it is not supplied by this package. Every URL is required and must be a host root such as `https://dev.digiwells.no/`, because `WellAPIUtils` appends service paths such as `Well/api/` and `EarthVerticalDatum/api/`.

Example routing:

```razor
<Router AppAssembly="@typeof(App).Assembly"
        AdditionalAssemblies="new[] { typeof(OSDC.Drilling.Well.WebPages.WellMain).Assembly }">
    ...
</Router>
```

## Backup/restore behavior

The page calls the typed `BatchExportWellsAsync` and `BatchRestoreWellsAsync` clients. It supports all or selected backup, previews uploaded documents, validates format version and Well UUIDs client-side, requires an explicit collision/catalog policy, asks for confirmation, and displays structured server errors. Server-side validation and transactionality remain authoritative.

## Context and reference integration

Well edit, survey-run, and trajectory workflows use current Field, Cluster, Rig, and Earth Vertical Datum contracts to resolve position and depth references. The shared `Rotary table`/`RTE` choice is backed by the mean of `Rig.FixedPlatformProperties.DrillFloorDepth` only when the Well's Cluster is a fixed platform with a valid platform-Rig link. Field coordinate conversion uses the stateless `FieldCoordinateConversion/Forward` and `/Inverse` endpoints. The consuming host must configure reachable dependency services.

## Generated contracts

The project compiles `../ModelSharedOut/WellMergedModel.cs` as a linked source file. After REST contract changes, regenerate it using [../ModelSharedOut/README.md](../ModelSharedOut/README.md) before building or packaging WebPages.

## Build and package

```powershell
dotnet build WebPages\WebPages.csproj
```

`GeneratePackageOnBuild` is enabled, so the NuGet package is written under `WebPages/bin/<configuration>`. The package includes this README and static web assets.
