using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Writers;
using NUnit.Framework;
using OSDC.DotnetLibraries.Drilling.SemanticCatalogue;
using OSDC.DotnetLibraries.General.Math;
using OSDC.Drilling.Well.Model;
using OSDC.Drilling.Well.Service;
using OSDC.Drilling.Well.Service.Mcp;
using OSDC.Drilling.Well.Service.Mcp.Tools;
using Swashbuckle.AspNetCore.SwaggerGen;
using Model = OSDC.Drilling.Well.Model;

namespace OSDC.Drilling.Well.SemanticTests;

public class SemanticContractTests
{
    private const string Extension = SemanticMetadata.ExtensionName;

    private static JsonObject Rest(Type type)
    {
        var options = new SchemaGeneratorOptions { SchemaIdSelector = t => t.FullName! };
        options.SchemaFilters.Add(new SemanticSchemaFilter());
        var generator = new SchemaGenerator(options, new JsonSerializerDataContractResolver(new JsonSerializerOptions()));
        var repository = new SchemaRepository();
        generator.GenerateSchema(type, repository);
        using var text = new StringWriter();
        var writer = new OpenApiJsonWriter(text);
        repository.Schemas[type.FullName!].SerializeAsV3(writer);
        writer.Flush();
        return JsonNode.Parse(text.ToString())!.AsObject();
    }

    private static JsonObject Mcp(string name, bool output = true)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddWellRestMcpTools();
        using var provider = services.BuildServiceProvider();
        var tool = provider.GetServices<IMcpTool>().Single(t => t.Name == name);
        return (output ? tool.OutputSchema : tool.InputSchema).AsObject();
    }

    [Test]
    public void ResourceAndInheritedClassificationsPublishCuratedCatalogue()
    {
        var rest = Rest(typeof(Model.Well));
        var mcp = Mcp("well_get_by_id")["properties"]!["data"]!;
        Assert.That(rest[Extension]!["catalogueVersion"]!.GetValue<string>(), Is.EqualTo("0.16.0"));
        Assert.That(JsonNode.DeepEquals(rest[Extension], mcp[Extension]), Is.True);
        var category = Rest(typeof(Model.WellFeatureCategory));
        Assert.That(category[Extension]!["concept"]!.GetValue<string>(), Is.EqualTo(Concepts.FeatureCategory));
        Assert.That(category["properties"]!["IsExclusive"]![Extension]!["concept"]!.GetValue<string>(), Is.EqualTo(Concepts.CategoryExclusivity));
        var assignment = Rest(typeof(Model.WellFeatureAssignment));
        Assert.That(assignment["properties"]!["FromDate"]![Extension]!["role"]!.GetValue<string>(), Is.EqualTo(Concepts.ValidityStart));
        Assert.That(assignment["properties"]!["ToDate"]![Extension]!["role"]!.GetValue<string>(), Is.EqualTo(Concepts.ValidityEnd));
        Assert.That(assignment["properties"]!["FeatureOptionID"]![Extension]!["concept"]!.GetValue<string>(), Is.EqualTo(Concepts.ResourceIdentifier));
    }

    [Test]
    public void EveryPublishedBindingResolvesToReviewedVocabulary()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddWellRestMcpTools();
        using var provider = services.BuildServiceProvider();
        int count = 0;
        void Check(JsonNode? node)
        {
            if (node is JsonObject obj)
            {
                if (obj["catalogue"] != null)
                {
                    if (obj["concept"] != null)
                    {
                        count++;
                        Assert.That(obj["catalogueVersion"]!.GetValue<string>(), Is.EqualTo("0.16.0"));
                        Assert.That(obj["curationStatus"]!.GetValue<string>(), Is.EqualTo("Reviewed"));
                        Assert.That(SemanticCatalogue.Default.Get(obj["concept"]!.GetValue<string>()).Status, Is.EqualTo(CurationStatus.Reviewed));
                    }
                }
                foreach (var child in obj) Check(child.Value);
            }
            else if (node is JsonArray array) foreach (var child in array) Check(child);
        }
        foreach (var tool in provider.GetServices<IMcpTool>()) { Check(tool.InputSchema); Check(tool.OutputSchema); }
        Assert.That(count, Is.GreaterThan(100));
    }

}
