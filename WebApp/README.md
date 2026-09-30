# OSDC.Drilling.Well.WebApp

The WebApp is the .NET 8 Blazor Server host for the reusable Well pages and selected pages from related OSDC microservices. It uses MudBlazor and is mounted under `/Well/webapp`.

## Run locally

`appsettings.Development.json` targets `https://dev.digiwells.no/`. To use a local Well service, override the URL before starting:

```powershell
$env:WellHostURL = "https://localhost:5001/"
dotnet run --project WebApp
```

- HTTPS: `https://localhost:5011/Well/webapp/Well`
- HTTP: `http://localhost:5012/Well/webapp/Well`

The backend URL is a host root, not `/Well/api`; each generated client adds its own service base path.

## Configuration

The host reads these keys from appsettings or environment variables:

- `WellHostURL`
- `ClusterHostURL`
- `FieldHostURL`
- `RigHostURL`
- `TrajectoryHostURL`

For Kubernetes production, `TrajectoryHostURL` is
`http://osdctrajectoryservice/`; the reusable pages append
`Trajectory/api/`.
- `EarthCartographicProjectionHostURL`
- `EarthGeodesyHostURL`
- `EarthGravityHostURL`
- `EarthMagneticFieldHostURL`
- `EarthVerticalDatumHostURL`
- `UnitConversionHostURL`

Development settings point to the shared development host. Production settings use Kubernetes service DNS names. Keep all values aligned with the selected environment.

## Navigation and routes

The left navigation is grouped like Field and Cluster:

- **Home**: Well-specific landing page and shortcuts to the main workflows.
- **Well Management**: Well, Well Features, and Well Identities.
- **Import/Export**: collapsed group containing Backup / Restore.
- **Survey Display**: Well Trajectories and Well Survey Runs.
- **Contextual Data**: Cluster, Field, and Rig.
- **Calculators**: cartographic conversion, vertical datum, gravity, and magnetic field.
- **Monitoring**: expanded usage statistics.

Principal Well routes:

| Page | Route |
| --- | --- |
| Well home | `/Well/webapp/Home` |
| Well management | `/Well/webapp/Well` |
| Backup and restore | `/Well/webapp/WellBackupRestore` |
| Identity definitions | `/Well/webapp/WellIdentities` |
| Feature categories | `/Well/webapp/WellFeatures` |
| Trajectories | `/Well/webapp/WellTrajectories` |
| Survey runs | `/Well/webapp/WellSurveyRuns` |
| Usage statistics | `/Well/webapp/StatisticsWell` |

The trajectory and survey-run pages provide searchable Field/Cluster/Well selectors and apply the shared unit system to depth and horizontal position. Position may be displayed relative to WGS84, the owning Field's cartographic projection, the selected Field or Cluster reference point, or the selected Well-head slot.

Earth Geodesy's Geodetic Datum and Spheroid pages, plus the vertical datum, gravity, and magnetic-field calculators, use local wrapper components in `WebApp/Pages`. This exposes only the required pages; registering those complete external Razor assemblies would import foreign or duplicate `/Home` routes and make the Blazor route table incorrect or ambiguous.

## Related WebPages integrations

`ExternalWebPagesServiceCollectionExtensions` registers configuration and API utilities for Cluster, Field, Rig, Earth Cartographic Projection, Earth Geodesy, Earth Gravity, Earth Magnetic Field, and Earth Vertical Datum. `ExternalRazorAssemblies` lists only assemblies whose complete route sets are safe to import.

Current package versions are defined in `WebApp.csproj`; do not duplicate version numbers in deployment scripts.

## Important files

- `Program.cs`: Blazor Server services, related API configuration, forwarded headers, and `/Well/webapp` path base.
- `App.razor`: router and approved additional assemblies.
- `ExternalRazorAssemblies.cs`: imported route assemblies.
- `ExternalWebPagesServiceCollectionExtensions.cs`: dependency-injection registrations for external pages.
- `Shared/NavMenu.razor`: grouped left navigation.
- `Pages/Home.razor`: Well-specific landing page.
- `Pages/GeodeticDatumPage.razor`, `SpheroidPage.razor`, and `*CalculatorPage.razor`: conflict-free wrappers for selected external pages.
- `appsettings.Development.json` / `appsettings.Production.json`: service host roots.

Well-specific Razor pages and generated-client utilities live in the `WebPages` project; see [../WebPages/README.md](../WebPages/README.md).

## Docker and Helm

Build from the repository root:

```powershell
docker build -t digiwells/osdcdrillingwellwebappclient:local -f WebApp/Dockerfile .
docker run --rm -p 5012:8080 `
  -e WellHostURL=https://host.docker.internal:5001/ `
  digiwells/osdcdrillingwellwebappclient:local
```

Chart: `WebApp/charts/osdcdrillingwellwebappclient`.

```powershell
helm upgrade --install osdcdrillingwellwebappclient WebApp/charts/osdcdrillingwellwebappclient `
  --kube-context dev-context --namespace default
```

The chart defaults to `docker.io/digiwells/osdcdrillingwellwebappclient:stable` with `image.pullPolicy: Always`.

## Build

```powershell
dotnet build WebApp\WebApp.csproj
```

Warnings in unrelated legacy page code should not be mistaken for route or host-configuration errors; builds must still complete with zero errors.

## SemanticCatalogue 0.9.0

The host consumes regenerated clients from the SemanticCatalogue 0.9.0 contracts. Existing routes, configuration and persistence are unchanged.


## Canonical reference adoption (0.9.0)

The provider registry uses the shared SemanticMetadata.Create factory and OSDC canonical drilling profile. A contradictory explicit reference fails. Along-hole and vertical coordinates have distinct references; uncertainties remain origin-free. Publish SemanticCatalogue 0.9.0 before CI or Docker restore. Local verification uses a packed 0.9.0 package without a permanent local-feed configuration.


Canonical reference metadata describes storage and REST/MCP payloads (`referenceScope: canonical-storage-and-api`), not a restriction on display choices (`presentationReferencesAllowed: true`). Web editors convert between the canonical reference and the supported reference selected by the user. Reference changes apply to coordinate values, not their standard uncertainties.
