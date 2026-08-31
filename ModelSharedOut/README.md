# ModelSharedOut

This project generates the client-facing C# contract and merged OpenAPI document used by Well consumers.

## Authoritative schema inputs

- `ClusterModel.json`: `Cluster/Service/wwwroot/json-schema/ClusterMergedModel.json`
- `FieldModel.json`: `Field/Service/wwwroot/json-schema/FieldMergedModel.json`
- `RigModel.json`: `Rig/Service/wwwroot/json-schema/RigMergedModel.json`
- `VerticalDatumModel.json`: `EarthVerticalDatum/Service/wwwroot/json-schema/EarthVerticalDatumMergedModel.json`
- `TrajectoryModel.json`: `Trajectory/Service/wwwroot/json-schema/TrajectoryMergedModel.json`
- `WellBoreModel.json`: `WellBore/Service/wwwroot/json-schema/WellBoreMergedModel.json`
- `WellFullName.json`: generated from the Well Service Debug build.

After refreshing inputs, run `dotnet build Service/Service.csproj --configuration Debug`, then run `dotnet run --project ModelSharedOut` and confirm overwrite. The generator writes `WellMergedModel.cs` and `Service/wwwroot/json-schema/WellMergedModel.json`. Commit all schema, client, and bundle changes together, then build the full solution.
