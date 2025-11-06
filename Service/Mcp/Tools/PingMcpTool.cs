using System;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using NORCE.Drilling.Well.Service.Mcp;

namespace NORCE.Drilling.Well.Service.Mcp.Tools;

public sealed class PingMcpTool : IMcpTool
{
    public string Name => "ping";

    public string Description => "Returns a pong response so clients can verify MCP connectivity.";

    public JsonNode? InputSchema => null;

    public Task<JsonNode?> InvokeAsync(JsonObject? arguments, CancellationToken cancellationToken)
    {
        var payload = new JsonObject
        {
            ["message"] = "pong",
            ["timestamp"] = DateTimeOffset.UtcNow.ToString("O")
        };

        return Task.FromResult<JsonNode?>(payload);
    }
}
