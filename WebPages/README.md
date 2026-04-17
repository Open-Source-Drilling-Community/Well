# NORCE.Drilling.Well.WebPages

`NORCE.Drilling.Well.WebPages` is a Razor class library that packages the `WellMain`, `WellEdit`, and `StatisticsMain` pages together with the page utilities and plotting component they require.

## Contents

- `WellMain`
- `WellEdit`
- `StatisticsMain`
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
    UnitConversionHostURL = builder.Configuration["UnitConversionHostURL"] ?? string.Empty
});
builder.Services.AddSingleton<IWellAPIUtils, WellAPIUtils>();
```

Example routing:

```razor
<Router AppAssembly="@typeof(App).Assembly"
        AdditionalAssemblies="new[] { typeof(NORCE.Drilling.Well.WebPages.WellMain).Assembly }">
```
