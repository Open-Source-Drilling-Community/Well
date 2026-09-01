# OSDC.Drilling.Well.Service

The Service is the .NET 8 backend for Well records, identity definitions, feature categories, backup/restore, usage statistics, Swagger, and MCP. It is mounted under `/Well/api` and persists to SQLite.

## Run locally

```powershell
dotnet build Service\Service.csproj
dotnet run --project Service
```

The launch profile listens on:

- `https://localhost:5001/Well/api`
- `http://localhost:5002/Well/api`
- Swagger UI: `https://localhost:5001/Well/api/swagger`
- Merged OpenAPI: `https://localhost:5001/Well/api/swagger/merged/swagger.json`

Override the listener when needed:

```powershell
$env:ASPNETCORE_URLS = "http://0.0.0.0:8080"
dotnet run --project Service --no-launch-profile
```

## REST API

Paths below are relative to `/Well/api`.

| Resource | Operations |
| --- | --- |
| `/Well` | List IDs, create a Well. |
| `/Well/MetaInfo` | List Well metadata. |
| `/Well/{id}` | Get, replace, or delete a Well. |
| `/Well/HeavyData` | List complete Wells. |
| `/Well/SlotId?slotId={id}` | List Wells assigned to a Slot. |
| `/Well/ClusterId?clusterId={id}` | List Wells assigned to a Cluster. |
| `/Well/UsedSlot/{clusterId}` | List Slot metadata referenced by a Cluster's Wells. |
| `/Well/BatchExport` | Export all Wells or an ordered selection with referenced local catalogs. |
| `/Well/BatchRestore` | Validate and atomically restore a versioned export document. |
| `/WellIdentity` | Identity definition ID listing and create. |
| `/WellIdentity/MetaInfo`, `/HeavyData`, `/{id}` | Identity metadata, complete listing, get, concurrency-checked replace, and delete. |
| `/WellFeatureCategory` | Feature-category ID listing and create. |
| `/WellFeatureCategory/MetaInfo`, `/HeavyData`, `/{id}` | Category metadata, complete listing, get, concurrency-checked replace, and delete. |
| `/WellUsageStatistics` | Current daily Well endpoint counters. |

Catalog updates require the `expectedModifiedUtc` query parameter and reject stale changes. Deleting a referenced definition, or removing a referenced feature option, returns a structured conflict rather than cascading into Well data.

Example:

```powershell
$base = "https://localhost:5001/Well/api"
$id = [guid]::NewGuid()

Invoke-RestMethod "$base/Well/HeavyData"
Invoke-RestMethod "$base/Well" -Method Post -ContentType "application/json" -Body (@{
    MetaInfo = @{ ID = $id }
    Name = "Example Well"
} | ConvertTo-Json -Depth 20)
Invoke-RestMethod "$base/Well/$id" -Method Delete
```

## Backup and restore

The backup is logical, portable JSON rather than a raw SQLite replacement. Format version 1 contains complete Wells plus only the referenced Well Identity and Feature Category definitions/options. Cluster and Slot UUIDs remain external references.

Restore policies:

- `FailIfExists`: reject the complete operation if any Well UUID already exists.
- `ReplaceExisting`: replace only matching Well UUIDs and create the rest.
- `MapExisting`: resolve local catalog definitions by compatible UUID or unique normalized name; reject missing definitions.
- `MapOrCreateMissing`: perform the same mapping and create missing local definitions/options with local UUIDs.

Validation, catalog mapping/creation, reference rewriting, and all Well writes use one SQLite transaction. Any validation error, ambiguity, collision, or storage failure rolls back the complete restore.

## SQLite schema and migrations

The default connection points to `../home/Well.db` relative to the process working directory. In the container, `/home` is a declared volume.

Current schema version: 1.

- `WellTable`
- `WellIdentityTable`
- `WellFeatureCategoryTable`

Startup upgrades a legacy version-0 database by creating only the missing catalog tables and setting `PRAGMA user_version` inside one transaction. Existing `WellTable` rows are preserved. The service never drops tables to repair a database. A newer version, unknown table, missing table, or malformed expected table causes startup to fail without destructive modification.

Operational safeguards:

- Back up or snapshot `/home/Well.db` before deploying a build that may migrate the schema.
- Never run two service writers against the same SQLite volume.
- Keep the Kubernetes service at one replica with deployment strategy `Recreate`.
- Preserve and reuse the existing PVC; do not replace it during a Helm identity or release-name change.

See [../deployment/identity-cutover.md](../deployment/identity-cutover.md) for the reviewed Kubernetes procedure.

## MCP contract

The service publishes 26 non-statistics REST operations as MCP tools plus `ping`. Usage statistics are intentionally not exposed through MCP.

- Streamable HTTP: `/well/api/mcp`
- WebSocket: `/well/api/mcp/ws`
- Authentication: none in the service itself
- Optional MCP-hub registration: `McpHub` configuration section; disabled by default

Tool families:

- `well_*`: Well queries, CRUD, batch export, and batch restore.
- `well_identity_*`: complete Identity definition CRUD and discovery.
- `well_feature_category_*`: complete Feature Category CRUD and discovery.

Every tool publishes a title, detailed description, closed input/output JSON schemas, and read-only/destructive/idempotent/open-world annotations. UUID arguments must be non-empty. Catalog update tools require `expectedModifiedUtc`. Tests compare all non-statistics controller actions with registered MCP tools to prevent REST/MCP drift.

## Docker and Helm

Build from the repository root:

```powershell
docker build -t digiwells/osdcdrillingwellservice:local -f Service/Dockerfile .
docker run --rm -p 5000:8080 -v wellsvc_home:/home digiwells/osdcdrillingwellservice:local
```

Chart: `Service/charts/osdcdrillingwellservice`.

```powershell
helm upgrade --install osdcdrillingwellservice Service/charts/osdcdrillingwellservice `
  --kube-context dev-context --namespace default
```

Chart defaults use `docker.io/digiwells/osdcdrillingwellservice:stable`, `image.pullPolicy: Always`, one replica, `Recreate`, and persistent claim `well-claim`. Prefer an immutable tag or digest for controlled releases.

## Generated OpenAPI

A Debug build runs the `CreateSwaggerJson` target and writes `ModelSharedOut/json-schemas/WellFullName.json`. After REST contract changes, follow [../ModelSharedOut/README.md](../ModelSharedOut/README.md) to regenerate the typed client and merged service schema.

## Security

CORS is permissive and the service has no built-in authentication or authorization. Protect REST, Swagger, MCP, SQLite storage, and backups through deployment-level controls.
