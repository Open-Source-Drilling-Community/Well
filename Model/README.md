# Model (NORCE.Drilling.Well.Model)

The Model project contains the domain classes used by the Well microservice and its clients. It provides a simple, serializable representation of a Well and a small utility for tracking API usage over time. These types are shared by the Service layer (Web API) and tests, and are the source for generated API documentation (DocFX).

## Purpose
- Define the Well data structure exchanged through the API and stored by the service.
- Offer lightweight usage statistics helpers for counting endpoint calls per day.
- Serve as a stable contract between the Service and its consumers (e.g., WebApp, tests).

## Key Types
- `Well`: DTO with metadata and relations.
  - Properties: `MetaInfo` (from `OSDC.DotnetLibraries.General.DataManagement`), `Name`, `Description`, `CreationDate`, `LastModificationDate`, `SlotID`, `ClusterID`, `IsSingleWell`.
- `UsageStatisticsWell`: singleton that aggregates daily counters for common Well endpoints and periodically persists them to `..\home\history.json`.
  - Helpers: `IncrementGetAllWellIdPerDay()`, `IncrementGetAllWellMetaInfoPerDay()`, `IncrementGetWellByIdPerDay()`, `IncrementGetAllWellPerDay()`, `IncrementPostWellPerDay()`, `IncrementPutWellByIdPerDay()`, `IncrementDeleteWellByIdPerDay()`.

## Target Framework
- .NET 8 (`net8.0`) with nullable reference types enabled.

## Dependencies
- `OSDC.DotnetLibraries.Drilling.DrillingProperties` — drilling domain abstractions used across the solution.
- `OSDC.DotnetLibraries.General.Common` — common utilities.
- `OSDC.DotnetLibraries.General.DataManagement` — includes `MetaInfo`, used by `Well`.
- `OSDC.DotnetLibraries.General.Statistics` — general statistics helpers.

Exact versions are specified in `Model.csproj`.

## How It Fits in the Solution
- Service (`Service` project): Controllers accept and return `NORCE.Drilling.Well.Model.Well`. Managers serialize/deserialize `Well` for persistence (SQLite). See `Service/Controllers/WellController.cs` and `Service/Managers/WellManager.cs`.
- Web client (`WebApp`): Calls the Service API that exposes `Well` payloads; it does not reference `Model` directly.
- Tests (`ModelTest`, `ServiceTest`): Use `Well` as the DTO under test for model behavior and service endpoints.
- Documentation: DocFX config (`Model/docfx.json`, `Model/articles`, `Model/api`) enables API/guide generation for this project.

## Usage Examples

### Create a Well
```csharp
using NORCE.Drilling.Well.Model;
using OSDC.DotnetLibraries.General.DataManagement;

var well = new Well
{
    MetaInfo = new MetaInfo { ID = Guid.NewGuid() },
    Name = "My Test Well",
    Description = "Example well payload",
    CreationDate = DateTimeOffset.UtcNow,
    ClusterID = Guid.NewGuid(),
    SlotID = Guid.NewGuid(),
    IsSingleWell = false
};
```

### Serialize/Deserialize
```csharp
using System.Text.Json;
using NORCE.Drilling.Well.Model;

string json = JsonSerializer.Serialize(well);
Well? roundTripped = JsonSerializer.Deserialize<Well>(json);
```

### Record Usage Statistics
```csharp
using NORCE.Drilling.Well.Model;

// Increment when serving a corresponding API call
UsageStatisticsWell.Instance.IncrementGetAllWellIdPerDay();

// Adjust the backup cadence if needed (default is 5 minutes)
UsageStatisticsWell.Instance.BackUpInterval = TimeSpan.FromMinutes(1);
```
Notes:
- The statistics singleton periodically writes to `..\home\history.json` (relative to the Service working directory).
- Persistence is managed automatically whenever an increment occurs and the backup interval has elapsed.

## Build and Reference
- Included in `Well.sln`. Build from the solution root:
  - `dotnet build`
- Add a project reference from another project (example):
  - `<ProjectReference Include="..\Model\Model.csproj" />`
  - or `dotnet add <your_project>.csproj reference ..\Model\Model.csproj`

## Tests
- See `ModelTest/WellTests.cs` for basic model behavior checks (constructors and property setters/getters).

## Documentation
- DocFX config is included under `Model/docfx.json` with API and articles scaffolding (`Model/api`, `Model/articles`).
- If you use DocFX locally, you can generate docs for this project by pointing DocFX to that config.

## Folder Structure (Model)
- `Well.cs` — core Well DTO.
- `UsageStatisticsWell.cs` — API usage counters and persistence.
- `Model.csproj` — project file and package references.
- `docfx.json`, `api/`, `articles/` — documentation configuration and placeholders.
