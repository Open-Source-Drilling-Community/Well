# Well — Full Solution

Microservice + Web UI for managing Well data. The solution includes a shared model, a REST API (Service), a Blazor Server client (WebApp), and supporting code generation for typed clients.

## Purpose
- Provide a consistent Well domain model across backend and clients.
- Expose CRUD endpoints for Well data via an ASP.NET Core Web API.
- Offer a Blazor Server UI to browse, create, edit, and delete wells and view usage statistics.

## Live Examples
- Service (Swagger UI): https://dev.digiwells.no/Well/api/swagger
- Service (API base): https://dev.digiwells.no/Well/api/Well
- WebApp (UI): https://dev.digiwells.no/Well/webapp/Well

## Projects
- Model: Well DTOs and usage statistics helpers. See `Model/README.md`.
- Service: ASP.NET Core Web API + SQLite persistence + Swagger UI. See `Service/README.md`.
- WebApp: Blazor Server UI using MudBlazor and generated API client. See `WebApp/README.md`.
- ModelSharedOut: Generates the OpenAPI client and merged schema consumed by WebApp/ServiceTest.
- ModelTest, ServiceTest: Unit and API tests for model and service.

## Install & Run Locally

Prerequisites
- .NET 8 SDK installed
- Optional: Docker 24+ and Helm if containerizing/deploying

Build
- From the repository root:
  - `dotnet restore`
  - `dotnet build`

Run the Service
- `dotnet run --project Service`
- Open Swagger UI: `http://localhost:5000/Well/api/swagger`
- API base: `http://localhost:5000/Well/api/Well`

Run the WebApp
- `dotnet run --project WebApp`
- Open UI: `https://localhost:5011/Well/webapp/Well` (or `http://localhost:5012/Well/webapp/Well`)
- Ensure the WebApp can reach the Service:
  - Default dev setting uses `WellHostURL=https://localhost:5001/` (see `WebApp/appsettings.Development.json`).
  - Override via env var: `WellHostURL=https://localhost:5001/ dotnet run --project WebApp`.

Useful Notes
- Paths are mounted under `/Well/api` (Service) and `/Well/webapp` (WebApp). If using a reverse proxy, route accordingly.
- The Service persists data in a SQLite DB at `../home/Well.db` relative to its working directory. Ensure write access.

## Usage Examples

Service (cURL)
- List IDs: `curl http://localhost:5000/Well/api/Well`
- MetaInfo list: `curl http://localhost:5000/Well/api/Well/MetaInfo`
- Get by ID: `curl http://localhost:5000/Well/api/Well/{id}`
- Full list: `curl http://localhost:5000/Well/api/Well/HeavyData`
- Create: `curl -X POST http://localhost:5000/Well/api/Well -H "Content-Type: application/json" -d '{"MetaInfo":{"ID":"<guid>"},"Name":"My Well"}'`
- Update: `curl -X PUT http://localhost:5000/Well/api/Well/{id} -H "Content-Type: application/json" -d '{"MetaInfo":{"ID":"{id}"},"Name":"Updated"}'`
- Delete: `curl -X DELETE http://localhost:5000/Well/api/Well/{id}`

WebApp
- Browse to the Well page and use the grid to add/edit/delete.
- Usage statistics available at `/Well/webapp/Statistics`.

## Dependencies (selected)
- Service
  - `Microsoft.Data.Sqlite`: SQLite provider
  - `Swashbuckle.AspNetCore.*`, `Microsoft.OpenApi.*`: OpenAPI docs and UI
  - Project ref: `Model`
- Model
  - `OSDC.DotnetLibraries.General.*`, `OSDC.DotnetLibraries.Drilling.DrillingProperties`
- WebApp
  - `MudBlazor`, `Plotly.Blazor`, `OSDC.UnitConversion.DrillingRazorMudComponents`
  - Project ref: `ModelSharedOut`
- ModelSharedOut
  - `NSwag.CodeGeneration.CSharp`, `Microsoft.OpenApi.Readers`

See each project’s `.csproj` for exact versions.

## Docker & Helm (optional)
- Service image/Dockerfile: `Service/Dockerfile` (exposes 8080, mounts `/home` for persistence)
- WebApp image/Dockerfile: `WebApp/Dockerfile`
- Helm charts: `Service/charts/norcedrillingwellservice`, `WebApp/charts/norcedrillingwellwebappclient`

## Security & Data
- SQLite database stored as clear text in the container volume. No authentication/authorization is included by default. Secure behind your ingress/proxy and network controls as required.

## More
- Per‑project guides: `Model/README.md`, `Service/README.md`, `WebApp/README.md`
- Templates and docs: https://github.com/NORCE-DrillingAndWells/Templates and https://github.com/NORCE-DrillingAndWells/DrillingAndWells/wiki
