# Service (NORCE.Drilling.Well.Service)

ASP.NET Core Web API that exposes CRUD endpoints for Well resources, persists data to SQLite, and serves an OpenAPI/Swagger UI. The service is the backend of the solution and the primary integration point for the Web client and external consumers.

## Purpose
- Host REST endpoints for Well operations under the base path `/Well/api`.
- Persist Well data in a local SQLite database located under `../home/Well.db`.
- Serve a merged OpenAPI document and Swagger UI for easy exploration/testing.
- Act as the contract boundary for clients (e.g., WebApp) using the shared `Model` DTOs.

## Installation

Prerequisites
- .NET 8 SDK
- Optional: Docker 24+ if containerizing

Local Run
1) Restore/build from the solution root:
   - `dotnet restore`
   - `dotnet build`
2) Run the service:
   - `dotnet run --project Service`
3) Browse the API:
   - Swagger UI: `http://localhost:5000/Well/api/swagger`
   - API base: `http://localhost:5000/Well/api/Well`

Notes
- The SQLite database and service state are stored under `../home` relative to the Service working directory. Ensure the process has write access.
- The app listens on the typical Kestrel defaults. Override with `ASPNETCORE_URLS`, for example:
  - `ASPNETCORE_URLS=http://0.0.0.0:5000 dotnet run --project Service`
- Reverse proxy support is enabled via forwarded headers; the app uses `UsePathBase("/Well/api")`.

Docker
1) Build:
   - `docker build -t digiwells/norcedrillingwellservice:local -f Service/Dockerfile .`
2) Run (persisting the `/home` volume for SQLite data):
   - `docker run --rm -p 5000:8080 -v wellsvc_home:/home digiwells/norcedrillingwellservice:local`
3) Open:
   - Swagger UI: `http://localhost:5000/Well/api/swagger`

Helm (Kubernetes)
- A chart is provided under `Service/charts/norcedrillingwellservice`.
- Example install with defaults:
  - `helm upgrade --install well-svc Service/charts/norcedrillingwellservice`

## Usage Examples

Base URL: `/Well/api`

Swagger
- UI: `/Well/api/swagger`
- Served JSON (merged): `/Well/api/swagger/merged/swagger.json`

Well endpoints (`Service/Controllers/WellController.cs`)
```bash
# List all well IDs
curl http://localhost:5000/Well/api/Well

# List all well MetaInfo
curl http://localhost:5000/Well/api/Well/MetaInfo

# Get a well by ID
curl http://localhost:5000/Well/api/Well/{id}

# Get all wells (full data)
curl http://localhost:5000/Well/api/Well/HeavyData

# Get used slots MetaInfo for a cluster
curl http://localhost:5000/Well/api/Well/UsedSlot/{clusterId}

# Create a well
curl -X POST http://localhost:5000/Well/api/Well \
  -H "Content-Type: application/json" \
  -d '{
        "MetaInfo": { "ID": "d0e5b56f-6a3e-4d3a-a5cf-3f7a4ab3a11" },
        "Name": "My Well",
        "Description": "Example",
        "CreationDate": "2024-01-01T00:00:00Z"
      }'

# Update a well by ID
curl -X PUT http://localhost:5000/Well/api/Well/{id} \
  -H "Content-Type: application/json" \
  -d '{ "MetaInfo": { "ID": "{id}" }, "Name": "Updated" }'

# Delete a well by ID
curl -X DELETE http://localhost:5000/Well/api/Well/{id}
```

Usage statistics (`Service/Controllers/WellUsageStatisticsController.cs`)
```bash
curl http://localhost:5000/Well/api/WellUsageStatistics
```

## Dependencies
- Runtime
  - `Microsoft.Data.Sqlite` — SQLite database driver used by `SqlConnectionManager` and `WellManager`.
  - `Swashbuckle.AspNetCore.SwaggerGen` and `Swashbuckle.AspNetCore.SwaggerUI` — OpenAPI generation and UI.
  - `Microsoft.OpenApi` and `Microsoft.OpenApi.Readers` — read and serve a pre-merged OpenAPI document.
- Project reference
  - `Model` — provides the `NORCE.Drilling.Well.Model.Well` DTO and usage statistics types.

See `Service.csproj` for exact versions and build targets. A `CreateSwaggerJson` target runs on Debug builds to export schema artifacts for `ModelSharedOut`.

## Integration in the Solution
- Uses `Model` DTOs to accept and return Well payloads across endpoints.
- Persists to SQLite via `SqlConnectionManager` and `WellManager` under `../home/Well.db`.
- Serves an OpenAPI document from `Service/wwwroot/json-schema/WellMergedModel.json` using `SwaggerMiddlewareExtensions`.
- Consumed by the Web client (`WebApp`), which calls these endpoints and renders results. Tests under `ServiceTest` exercise controller behaviors against the API.

## Configuration & Behavior
- Base path: `/Well/api` (set with `UsePathBase`).
- CORS: permissive default allowing any origin/headers/methods (with credentials).
- Forwarded headers: supports `X-Forwarded-Proto`/`X-Forwarded-Host` for reverse proxies.
- JSON: uses `System.Text.Json` with custom options from `JsonSettings` (preserves property casing, enum strings).
- Static files: enabled to serve Swagger JSON and any assets under `wwwroot`.

## Quick Links
- Docker image name: `digiwells/norcedrillingwellservice`
- Swagger (dev): https://dev.digiwells.no/Well/api/swagger
- Swagger (prod): https://app.digiwells.no/Well/api/swagger
- API base (dev): https://dev.digiwells.no/Well/api/Well
- API base (prod): https://app.digiwells.no/Well/api/Well

---

This solution was generated from the NORCE Drilling and Wells .NET template. See https://github.com/NORCE-DrillingAndWells/Templates and the related documentation at https://github.com/NORCE-DrillingAndWells/DrillingAndWells/wiki/.NET-Templates
