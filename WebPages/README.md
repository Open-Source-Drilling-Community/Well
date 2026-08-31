# OSDC.Drilling.Well.WebPages

`OSDC.Drilling.Well.WebPages` is a Razor class library that packages the `WellMain`, `WellEdit`, `WellSurveyRuns`, `WellTrajectories`, and `StatisticsWell` pages together with their API and plotting support.

## Contents

- `WellMain`
- `WellEdit`
- `WellSurveyRuns`
- `WellTrajectories`
- `StatisticsWell`
- `ScatterPlot`
- Well page support classes such as API access helpers and unit/reference helpers

## Dependencies

The package depends on:

- `ModelSharedOut`
- `OSDC.DotnetLibraries.Drilling.WebAppUtils`
- `MudBlazor`
- `OSDC.UnitConversion.DrillingRazorMudComponents`
- `Plotly.Blazor`

## Host application requirements

The consuming web app is expected to:

1. Reference this package.
2. Provide an implementation of `IWellWebPagesConfiguration`.
3. Register that configuration and `IWellAPIUtils` in dependency injection.
4. Include the library assembly in Blazor routing via `AdditionalAssemblies`.

Example registration:

```csharp
builder.Services.AddSingleton<IWellWebPagesConfiguration>(new WebPagesHostConfiguration
{
    WellHostURL = builder.Configuration["WellHostURL"] ?? string.Empty,
    ClusterHostURL = builder.Configuration["ClusterHostURL"] ?? string.Empty,
    FieldHostURL = builder.Configuration["FieldHostURL"] ?? string.Empty,
    RigHostURL = builder.Configuration["RigHostURL"] ?? string.Empty,
    TrajectoryHostURL = builder.Configuration["TrajectoryHostURL"] ?? string.Empty,
    EarthVerticalDatumHostURL = builder.Configuration["EarthVerticalDatumHostURL"] ?? string.Empty,
    UnitConversionHostURL = builder.Configuration["UnitConversionHostURL"] ?? string.Empty
});
builder.Services.AddSingleton<IWellAPIUtils, WellAPIUtils>();
```

Example routing:

```razor
<Router AppAssembly="@typeof(App).Assembly"
        AdditionalAssemblies="new[] { typeof(OSDC.Drilling.Well.WebPages.WellMain).Assembly }">
```

## Rig and Vertical Datum integration

The reusable pages retrieve Rig and Earth Vertical Datum information through the configured API utilities. `MslDepthReferenceUtils` is used by the well editor, survey-run page, and trajectory page to present consistent mean-sea-level depth references. This package uses `OSDC.DotnetLibraries.Drilling.WebAppUtils` 1.1.4.
