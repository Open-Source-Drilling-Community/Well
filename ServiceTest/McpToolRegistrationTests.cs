using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using NORCE.Drilling.Well.Service.Controllers;
using NORCE.Drilling.Well.Service.Mcp;
using NORCE.Drilling.Well.Service.Mcp.Tools;

namespace NORCE.Drilling.Well.ServiceTest;

[TestFixture]
public sealed class McpToolRegistrationTests
{
    private static readonly IReadOnlyDictionary<string, string> EndpointToolMap = new Dictionary<string, string>
    {
        ["GetAllWellId"] = "well_get_all_ids",
        ["GetAllWellMetaInfo"] = "well_get_all_meta_info",
        ["GetWellById"] = "well_get_by_id",
        ["GetAllWell"] = "well_get_all",
        ["GetAllWellBySlotId"] = "well_get_all_by_slot_id",
        ["GetAllWellByClusterId"] = "well_get_all_by_cluster_id",
        ["GetAllUsedSlotMetaInfoByClusterId"] = "well_get_used_slot_meta_info_by_cluster_id",
        ["PostWell"] = "well_create",
        ["PutWellById"] = "well_update_by_id",
        ["DeleteWellById"] = "well_delete_by_id"
    };

    private ServiceProvider _provider = null!;
    private IReadOnlyDictionary<string, IMcpTool> _tools = null!;

    [SetUp]
    public void SetUp()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLegacyMcpTool<PingMcpTool>();
        services.AddWellRestMcpTools();
        _provider = services.BuildServiceProvider();
        _tools = _provider.GetServices<IMcpTool>().ToDictionary(tool => tool.Name);
    }

    [TearDown]
    public void TearDown() => _provider.Dispose();

    [Test]
    public void Every_non_statistics_controller_endpoint_has_a_registered_tool()
    {
        var endpoints = typeof(WellController).GetMethods()
            .Where(method => method.GetCustomAttributes(typeof(HttpMethodAttribute), true).Length > 0)
            .Select(method => method.Name);
        Assert.That(endpoints, Is.EquivalentTo(EndpointToolMap.Keys));
        Assert.That(_tools.Keys, Is.EquivalentTo(EndpointToolMap.Values.Append("ping")));
    }

    [Test]
    public void Usage_statistics_are_not_exposed() => Assert.That(_tools.Keys, Has.None.Contains("statistics"));

    [Test]
    public void Protocol_tool_names_are_valid_and_unique()
    {
        string[] names = _provider.GetServices<McpServerTool>().Select(tool => tool.ProtocolTool.Name).ToArray();
        Assert.That(names, Has.Length.EqualTo(_tools.Count));
        Assert.That(names, Is.Unique);
        Assert.That(names.All(name => !name.Contains('.')), Is.True);
    }

    [Test]
    public void Rest_tools_have_detailed_descriptions()
    {
        foreach (string toolName in EndpointToolMap.Values)
        {
            Assert.That(_tools[toolName].Description, Has.Length.GreaterThan(100), toolName);
        }
    }

    [TestCase("well_get_all_ids")]
    [TestCase("well_get_all_meta_info")]
    [TestCase("well_get_all")]
    public void Parameterless_tools_publish_an_explicit_empty_object_schema(string toolName)
    {
        JsonObject schema = RequireObject(_tools[toolName].InputSchema);
        Assert.That(schema["type"]?.GetValue<string>(), Is.EqualTo("object"));
        Assert.That(schema["additionalProperties"]?.GetValue<bool>(), Is.False);
    }

    [Test]
    public void Create_tool_schema_describes_the_complete_well_payload()
    {
        JsonObject root = RequireObject(_tools["well_create"].InputSchema);
        Assert.That(RequiredNames(root), Is.EquivalentTo(new[] { "well" }));

        JsonObject well = Property(root, "well");
        Assert.That(RequiredNames(well), Does.Contain("MetaInfo"));
        Assert.That(PropertyNames(well), Is.EquivalentTo(new[]
        {
            "MetaInfo", "Name", "Description", "CreationDate", "LastModificationDate",
            "SlotID", "ClusterID", "IsSingleWell"
        }));
        Assert.That(well["additionalProperties"]?.GetValue<bool>(), Is.False);

        JsonObject metaInfo = Property(well, "MetaInfo");
        Assert.That(RequiredNames(metaInfo), Does.Contain("ID"));
        Assert.That(Property(metaInfo, "ID")["format"]?.GetValue<string>(), Is.EqualTo("uuid"));
        Assert.That(Property(well, "CreationDate")["format"]?.GetValue<string>(), Is.EqualTo("date-time"));
        Assert.That(Property(well, "SlotID")["format"]?.GetValue<string>(), Is.EqualTo("uuid"));
        Assert.That(Property(well, "ClusterID")["format"]?.GetValue<string>(), Is.EqualTo("uuid"));
    }

    [Test]
    public void Update_tool_schema_requires_matching_id_and_well_arguments()
    {
        JsonObject root = RequireObject(_tools["well_update_by_id"].InputSchema);
        Assert.That(RequiredNames(root), Is.EquivalentTo(new[] { "well", "id" }));
        Assert.That(Property(root, "id")["format"]?.GetValue<string>(), Is.EqualTo("uuid"));
        Assert.That(Property(root, "id")["description"]?.GetValue<string>(), Does.Contain("well.MetaInfo.ID"));
    }

    [TestCase("well_get_by_id")]
    [TestCase("well_get_all_by_slot_id")]
    [TestCase("well_get_all_by_cluster_id")]
    [TestCase("well_get_used_slot_meta_info_by_cluster_id")]
    public async Task Identifier_tools_require_their_identifier(string toolName)
    {
        JsonObject? response = await _tools[toolName].InvokeAsync(new JsonObject(), CancellationToken.None) as JsonObject;
        Assert.That(response?["status"]?.GetValue<int>(), Is.EqualTo(400));
    }

    [Test]
    public async Task Create_tool_requires_a_request_body()
    {
        JsonObject? response = await _tools["well_create"].InvokeAsync(new JsonObject(), CancellationToken.None) as JsonObject;
        Assert.That(response?["status"]?.GetValue<int>(), Is.EqualTo(400));
    }

    private static JsonObject RequireObject(JsonNode? node)
    {
        Assert.That(node, Is.TypeOf<JsonObject>());
        return (JsonObject)node!;
    }

    private static JsonObject Property(JsonObject schema, string name)
    {
        JsonObject properties = RequireObject(schema["properties"]);
        return RequireObject(properties[name]);
    }

    private static string[] PropertyNames(JsonObject schema)
    {
        return RequireObject(schema["properties"]).Select(property => property.Key).ToArray();
    }

    private static string[] RequiredNames(JsonObject schema)
    {
        Assert.That(schema["required"], Is.TypeOf<JsonArray>());
        return ((JsonArray)schema["required"]!).Select(node => node!.GetValue<string>()).ToArray();
    }
}
