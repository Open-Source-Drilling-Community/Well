using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace OSDC.Drilling.Well.Service.Mcp;

public static class McpServiceCollectionExtensions
{
    public static IServiceCollection AddLegacyMcpTool<TTool>(this IServiceCollection services)
        where TTool : class, IMcpTool
    {
        services.AddSingleton<TTool>();
        services.AddSingleton<IMcpTool>(sp => sp.GetRequiredService<TTool>());
        services.AddSingleton<McpServerTool>(sp =>
        {
            var tool = sp.GetRequiredService<TTool>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            return new LegacyMcpServerToolAdapter(tool, loggerFactory);
        });

        return services;
    }

    public static IServiceCollection AddLegacyMcpTool(
        this IServiceCollection services,
        string name,
        string description,
        JsonNode? inputSchema,
        JsonNode outputSchema,
        McpToolBehavior behavior,
        Func<IServiceProvider, JsonObject?, CancellationToken, Task<JsonNode?>> invokeAsync)
    {
        inputSchema ??= EmptyInputSchema();
        McpOperationSemantics.Apply(name, inputSchema);
        services.AddSingleton<IMcpTool>(sp => new DelegateMcpTool(
            name, description, inputSchema, outputSchema, behavior,
            (arguments, cancellationToken) => invokeAsync(sp, arguments, cancellationToken)));
        services.AddSingleton<McpServerTool>(sp => new LegacyMcpServerToolAdapter(
            sp.GetServices<IMcpTool>().Last(tool => tool.Name == name),
            sp.GetRequiredService<ILoggerFactory>()));
        return services;
    }

    private sealed class DelegateMcpTool : IMcpTool
    {
        private readonly Func<JsonObject?, CancellationToken, Task<JsonNode?>> _invokeAsync;

        public DelegateMcpTool(string name, string description, JsonNode inputSchema, JsonNode outputSchema,
            McpToolBehavior behavior,
            Func<JsonObject?, CancellationToken, Task<JsonNode?>> invokeAsync)
        {
            Name = name;
            Description = description;
            InputSchema = inputSchema;
            OutputSchema = outputSchema;
            Behavior = behavior;
            _invokeAsync = invokeAsync;
        }

        public string Name { get; }
        public string Description { get; }
        public JsonNode InputSchema { get; }
        public JsonNode OutputSchema { get; }
        public McpToolBehavior Behavior { get; }
        public Task<JsonNode?> InvokeAsync(JsonObject? arguments, CancellationToken cancellationToken) =>
            _invokeAsync(arguments, cancellationToken);
    }

    private static JsonNode EmptyInputSchema() => JsonNode.Parse("""{"type":"object","additionalProperties":false}""")!;
}

internal static class McpOperationSemantics
{
    public static void Apply(string name, JsonNode schema)
    {
        string concept = name.Contains("feature_assignment", StringComparison.Ordinal) ? OSDC.DotnetLibraries.Drilling.SemanticCatalogue.Concepts.FeatureAssignment
            : name.Contains("identity_assignment", StringComparison.Ordinal) ? OSDC.DotnetLibraries.Drilling.SemanticCatalogue.Concepts.IdentityAssignment
            : name.Contains("feature_category", StringComparison.Ordinal) ? OSDC.DotnetLibraries.Drilling.SemanticCatalogue.Concepts.FeatureCategory
            : name.Contains("identity", StringComparison.Ordinal) && !name.Contains("assignment", StringComparison.Ordinal) ? OSDC.DotnetLibraries.Drilling.SemanticCatalogue.Concepts.IdentityDefinition
            : OSDC.DotnetLibraries.Drilling.SemanticCatalogue.Concepts.Well;
        schema[OSDC.DotnetLibraries.Drilling.SemanticCatalogue.SemanticMetadata.ExtensionName] =
            OSDC.DotnetLibraries.Drilling.SemanticCatalogue.SemanticMetadata.Create(concept, Role(name), assertionSource: "provider-mcp-operation");
    }

    private static string Role(string name) =>
        name.Contains("validate", StringComparison.Ordinal) || name.Contains("audit", StringComparison.Ordinal)
            ? OSDC.DotnetLibraries.Drilling.SemanticCatalogue.Concepts.StatelessEvaluation
        : name.Contains("batch_export", StringComparison.Ordinal) || name.Contains("get_all", StringComparison.Ordinal) || name.EndsWith("_search", StringComparison.Ordinal)
            ? OSDC.DotnetLibraries.Drilling.SemanticCatalogue.Concepts.ResourceCollectionRetrieval
        : name.EndsWith("_get_by_id", StringComparison.Ordinal)
            ? OSDC.DotnetLibraries.Drilling.SemanticCatalogue.Concepts.ResourceRetrieval
        : name.Contains("batch_restore", StringComparison.Ordinal)
            ? OSDC.DotnetLibraries.Drilling.SemanticCatalogue.Concepts.ResourceOperation
        : name.EndsWith("_create", StringComparison.Ordinal) || name.EndsWith("_add", StringComparison.Ordinal)
            ? OSDC.DotnetLibraries.Drilling.SemanticCatalogue.Concepts.ResourceCreation
        : name.EndsWith("_update_by_id", StringComparison.Ordinal)
            ? OSDC.DotnetLibraries.Drilling.SemanticCatalogue.Concepts.ResourceReplacement
        : name.Contains("_update", StringComparison.Ordinal) || name.Contains("_patch", StringComparison.Ordinal) || name.Contains("_mutate", StringComparison.Ordinal)
            ? OSDC.DotnetLibraries.Drilling.SemanticCatalogue.Concepts.ResourcePartialUpdate
        : name.Contains("_delete", StringComparison.Ordinal)
            ? OSDC.DotnetLibraries.Drilling.SemanticCatalogue.Concepts.ResourceDeletion
        : OSDC.DotnetLibraries.Drilling.SemanticCatalogue.Concepts.ResourceOperation;
}
