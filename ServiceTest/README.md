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
