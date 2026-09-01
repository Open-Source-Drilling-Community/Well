# Well

The Well solution provides an ASP.NET Core microservice and Blazor Server UI for managing Wells, their identity assignments, and their feature assignments. It also includes versioned JSON backup/restore, SQLite persistence, OpenAPI client generation, usage statistics, and an MCP server.

## Projects

| Project | Purpose |
| --- | --- |
| `Model` | Authoritative Well, identity, feature, backup/restore, mutation-error, and usage-statistics contracts. |
| `Service` | REST API, transactional SQLite persistence and migration, Swagger, and MCP endpoints. |
| `ModelSharedOut` | Generates the merged OpenAPI document and typed C# clients used by consumers. |
| `WebPages` | Reusable Razor pages for Well management, catalogs, backup/restore, surveys, trajectories, and statistics. |
| `WebApp` | Blazor Server host for Well and related microservice pages. |
| `ModelTest` / `ServiceTest` | Model, controller, migration, backup/restore, and MCP contract tests. |
| `DBVersioningManager` | Database-versioning support utility. |

See the project guides: [Model](Model/README.md), [Service](Service/README.md), [ModelSharedOut](ModelSharedOut/README.md), [WebPages](WebPages/README.md), [WebApp](WebApp/README.md), and [ServiceTest](ServiceTest/README.md).

## Build and run locally

Prerequisites: .NET 8 SDK. Docker and Helm are optional.

```powershell
dotnet restore Well.sln
dotnet build Well.sln
```

Start the service using its launch profile:

```powershell
dotnet run --project Service
```

- HTTPS: `https://localhost:5001/Well/api`
- HTTP: `http://localhost:5002/Well/api`
- Swagger: `https://localhost:5001/Well/api/swagger`

Start the WebApp in another terminal:

```powershell
$env:WellHostURL = "https://localhost:5001/"
dotnet run --project WebApp
```

- HTTPS: `https://localhost:5011/Well/webapp/Well`
- HTTP: `http://localhost:5012/Well/webapp/Well`

`WebApp/appsettings.Development.json` targets the shared development environment. Set `WellHostURL` and any related service URLs as environment variables when testing against local services.

## Main capabilities

- Well CRUD and queries by Cluster or Slot.
- User-managed Well Identity definitions and per-Well identity values.
- User-managed Well Feature Categories, options, and validity-aware assignments.
- Versioned logical JSON backup of all Wells or an ordered selection.
- Atomic restore with conflict policies and catalog mapping/creation policies.
- Survey-run and trajectory displays with Rig and mean-sea-level depth-reference integration.
- Context pages for Field, Cluster, Rig, projections, geodetic datum, and spheroid data.
- Cartographic, vertical datum, gravity, and magnetic-field calculators.
- Per-endpoint usage-statistics dashboard.
- MCP access to every non-statistics REST operation.

Well and catalog replacement operations use optimistic concurrency: callers send the `LastModificationDate` from their latest read as `expectedModifiedUtc`. Stale writes return `409` and do not overwrite the newer record. MCP closed-object schemas are also enforced at runtime, including rejection of unknown nested fields.

## Data and upgrade safety

The service stores data in `../home/Well.db` relative to its working directory; the container mounts `/home`. Schema version 1 contains `WellTable`, `WellIdentityTable`, and `WellFeatureCategoryTable`.

Database startup migration is additive and transactional. It never drops an existing table or Well row. Unknown, newer, or malformed schemas stop startup without attempting destructive repair. Backup/restore also uses one SQLite transaction, so validation, catalog mapping, and all writes either commit together or roll back together.

Before deployment, keep an independent copy or storage snapshot of `/home/Well.db`. Kubernetes must not run overlapping service writers against the same SQLite volume. The service chart therefore uses `Recreate`, one replica, and a persistent claim. The reviewed identity-cutover procedure is in [deployment/identity-cutover.md](deployment/identity-cutover.md).

## Docker and Kubernetes

Images:

- `docker.io/digiwells/osdcdrillingwellservice`
- `docker.io/digiwells/osdcdrillingwellwebappclient`

Charts:

- `Service/charts/osdcdrillingwellservice`
- `WebApp/charts/osdcdrillingwellwebappclient`

Both charts currently default to tag `stable` with `image.pullPolicy: Always`. A rollout restart creates new pods and therefore checks the registry again, but immutable version or digest references are preferable for reproducible releases. Use `helm --kube-context <context> ...`; Helm does not accept a `--context` flag.

Configured ingress hosts are `dev.digiwells.no`, `app.digiwells.no`, and `awe.web.intra.norceresearch.no` under `/Well/api` and `/Well/webapp`.

## Tests

```powershell
dotnet test ModelTest\ModelTest.csproj
dotnet test ServiceTest\ServiceTest.csproj --filter "FullyQualifiedName!~McpServerHttpTests"
```

The two MCP HTTP tests require a running service at `http://localhost:8080/well/api/mcp`; see [ServiceTest/README.md](ServiceTest/README.md).

## Security

Authentication and authorization are not enabled by default. SQLite data is not encrypted by the service. Protect the API, WebApp, MCP endpoints, backups, and persistent volume through ingress, identity, network, and storage controls appropriate to the deployment.
