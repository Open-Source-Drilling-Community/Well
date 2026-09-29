# Well ServiceTest

`ServiceTest` validates REST controller behavior, catalog integrity, database migration, batch backup/restore, and the MCP contract.

## Coverage

- `WellControllerTests`: Well controller validation, CRUD behavior, external-reference validation, and deterministic audit pagination.
- `CatalogAndMigrationTests`: default Identity/Feature catalogs, reference protection, optimistic concurrency, additive schema migration, and preservation checks against captured Kubernetes database copies when available.
- `WellBatchBackupRestoreTests`: ordered export, dependency closure, catalog remapping/creation, collision rollback, corrupt-document rejection, and legacy-upgrade data preservation.
- `WellExternalReferenceValidatorTests`: Cluster/Slot membership checks, tri-state dependency failures, and per-batch Cluster-read caching.
- `McpToolRegistrationTests`: parity between all 35 non-statistics REST actions and MCP tools, operation-specific write/response schemas, strict inputs, bounded search/audit, detailed descriptions, and behavior annotations.
- `McpServerHttpTests`: live streamable-HTTP initialization, tool discovery, and `ping` invocation.

## Run without the live MCP tests

```powershell
dotnet test ServiceTest\ServiceTest.csproj --filter "FullyQualifiedName!~McpServerHttpTests"
```

## Run the complete suite

The HTTP tests connect to `http://localhost:8080/well/api/mcp`. Start the service in one terminal:

```powershell
dotnet run --project Service\Service.csproj --urls http://localhost:8080
```

Then run in another terminal:

```powershell
dotnet test ServiceTest\ServiceTest.csproj
```

Stop the service after the tests. Test databases are created under the test working directory; production data is not modified.

## Shared classification regression checks

The ResourceClassification 0.1.0 adoption is covered by ModelTest/ClassificationContractTests (stored JSON compatibility, nullable references, concrete options and interface conversion), plus the existing isolated catalogue, backup/restore, database safety and MCP registration tests. Live HTTP tests require a separately started test service. Well's optional Kubernetes-backup test skips when no local backup snapshots are available.
