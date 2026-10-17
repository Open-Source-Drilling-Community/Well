# Well ServiceTest

`ServiceTest` validates REST controller behavior, catalog integrity, database migration, batch backup/restore, and the MCP contract.

## Coverage

- `WellControllerTests`: Well controller validation, CRUD behavior, external-reference validation, and deterministic audit pagination.
- `CatalogAndMigrationTests`: default Identity/Feature catalogs, reference protection, optimistic concurrency, additive schema migration, and preservation checks against captured Kubernetes database copies when available.
- `WellBatchBackupRestoreTests`: ordered export, dependency closure, catalog remapping/creation, collision rollback, corrupt-document rejection, and legacy-upgrade data preservation.
- `WellExternalReferenceValidatorTests`: Cluster/Slot membership checks, tri-state dependency failures, and per-batch Cluster-read caching.
- `McpToolRegistrationTests`: parity between all 35 non-statistics REST actions and MCP tools, operation-specific write/response schemas, strict inputs, bounded search/audit, detailed descriptions, and behavior annotations.
- `McpServerHttpTests`: in-process Streamable HTTP initialization, tool discovery, and `ping` invocation.

## Run the complete suite

The HTTP tests host the service in process; no external service or listening port is required:

```powershell
dotnet test ServiceTest\ServiceTest.csproj
```

## Shared classification regression checks

The ResourceClassification 0.1.0 adoption is covered by ModelTest/ClassificationContractTests (stored JSON compatibility, nullable references, concrete options and interface conversion), plus the existing isolated catalogue, backup/restore, database safety and MCP registration tests. Well's optional Kubernetes-backup test skips when no local backup snapshots are available.

## SemanticCatalogue 0.18.0

`SemanticContractTests` verifies REST/MCP binding parity, inherited classification semantics and that published MCP bindings resolve to reviewed SemanticCatalogue 0.18.0 concepts.


## Canonical reference adoption (0.9.0)

Semantic contract tests verify the 0.9.0 bindings and reviewed concepts. WellBore also asserts the WGS84 path-intersection origin, rejects a vertical-reference substitution and retains separate uncertainty semantics.

## Bounded light discovery

Resource and catalog discovery returns light pages, with `offset` defaulting to 0 and `limit` defaulting to 50 (allowed range 1–200). Names and relevant resource-reference filters are evaluated on the server. Unbounded full-object `HeavyData` routes and tools, including filtered full-object lists, have been removed. Existing UUID and HTTP-metadata listings remain available; use named light pages for routine discovery.

REST routes and MCP tool names (relative to the service's existing API path base):

- `/Well/LightData` / `well_discover`
- `/WellFeatureCategory/LightData` / `well_feature_category_discover`
- `/WellIdentity/LightData` / `well_identity_discover`

Pages contain `Items`, `Offset`, `Limit`, `TotalCount`, and `NextOffset`; `NextOffset` is null on the final page. Offset pages are individually consistent reads, not a snapshot spanning requests: concurrent inserts/deletes can change later pages. Refresh discovery when the underlying collection changes. EarthGeodesy's existing search pages instead expose `Items[].Value`, `Offset`, `Limit`, and `TotalCount`.

Single-record reads, result/chunk reads, and deliberate backup/export/restore operations are preserved. Backups can still contain complete resources and should be treated as explicit bulk operations.

This is a breaking REST/MCP contract change: update generated clients, UIs, and external integrations together with the service release. Removed heavy-list names are not compatibility aliases. No persisted engineering shapes, database names, or storage volumes are changed. Historical usage counters are retained; bounded discovery has a separate additive `DiscoveryRequestsPerDay` history in resource services.


Contract checks cover removed heavy routes/tools, bounded pagination and strict invalid-argument rejection, and omission of engineering collections from discovery. Persistence tests exercise ordering, filters, totals, continuation, empty results, and retained selected reads. Run transport tests against isolated test storage.
