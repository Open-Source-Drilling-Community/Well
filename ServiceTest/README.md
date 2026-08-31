# Well ServiceTest

This project validates the Well service API and its MCP surface.

## MCP coverage

- `McpToolRegistrationTests.cs` checks that the ten Well REST tools and `ping` are registered and that usage-statistics operations are excluded.
- `McpServerHttpTests.cs` exercises MCP initialization, tool listing, and representative calls against a running service.

The live HTTP tests require the Well service at the configured test base URL. Run the suite with `dotnet test ServiceTest/ServiceTest.csproj`.
