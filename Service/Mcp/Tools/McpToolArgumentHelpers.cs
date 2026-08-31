using System;
using System.Text.Json.Nodes;

namespace OSDC.Drilling.Well.Service.Mcp.Tools;

internal static class McpToolArgumentHelpers
{
    public static JsonObject CreateEmptySchema()
    {
        return new JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = false
        };
    }

    public static JsonObject CreateGuidSchema(string key, string description)
    {
        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                [key] = new JsonObject
                {
                    ["type"] = "string",
                    ["format"] = "uuid",
                    ["description"] = description
                }
            },
            ["required"] = new JsonArray
            {
                key
            },
            ["additionalProperties"] = false
        };
    }

    public static JsonObject CreateWellSchema(bool includeId = false)
    {
        var properties = new JsonObject
        {
            ["well"] = CreateWellObjectSchema()
        };
        var required = new JsonArray { "well" };

        if (includeId)
        {
            properties["id"] = new JsonObject
            {
                ["type"] = "string",
                ["format"] = "uuid",
                ["description"] = "Identifier of the stored well to update. It must equal well.MetaInfo.ID."
            };
            required.Add("id");
        }

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required,
            ["additionalProperties"] = false
        };
    }

    public static JsonObject CreateWellResourceSchema() => CreateWellObjectSchema();

    public static JsonObject CreateStatusOnlyOutputSchema() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject { ["status"] = SuccessStatus() },
        ["required"] = new JsonArray("status"),
        ["additionalProperties"] = false
    };

    public static JsonObject CreateIdsOutputSchema() => SuccessEnvelope(new JsonObject
    {
        ["type"] = "array",
        ["items"] = new JsonObject { ["type"] = "string", ["format"] = "uuid" }
    });

    public static JsonObject CreateMetaInfoListOutputSchema() => SuccessEnvelope(new JsonObject
    {
        ["type"] = "array",
        ["items"] = CreateMetaInfoSchema()
    });

    public static JsonObject CreateWellOutputSchema() => SuccessEnvelope(CreateWellObjectSchema());

    public static JsonObject CreateWellListOutputSchema() => SuccessEnvelope(new JsonObject
    {
        ["type"] = "array",
        ["items"] = CreateWellObjectSchema()
    });

    private static JsonObject CreateWellObjectSchema()
    {
        return new JsonObject
        {
            ["type"] = "object",
            ["description"] = "Complete Well resource. MetaInfo.ID must be a non-empty UUID; the service does not generate an identifier.",
            ["properties"] = new JsonObject
            {
                ["MetaInfo"] = CreateMetaInfoSchema(),
                ["Name"] = NullableString("Human-readable well name."),
                ["Description"] = NullableString("Human-readable description of the well."),
                ["CreationDate"] = NullableDateTime("UTC or offset timestamp at which the well record was created."),
                ["LastModificationDate"] = NullableDateTime("UTC or offset timestamp of the most recent modification."),
                ["SlotID"] = NullableUuid("Identifier of the slot to which the well belongs."),
                ["ClusterID"] = NullableUuid("Identifier of the cluster to which the well belongs."),
                ["IsSingleWell"] = new JsonObject
                {
                    ["type"] = "boolean",
                    ["description"] = "True when the cluster is only a proxy for a standalone well.",
                    ["default"] = false
                }
            },
            ["required"] = new JsonArray { "MetaInfo" },
            ["additionalProperties"] = false
        };
    }

    private static JsonObject CreateMetaInfoSchema() => new()
    {
        ["type"] = "object",
        ["description"] = "Identity and optional HTTP location metadata for the well.",
        ["properties"] = new JsonObject
        {
            ["ID"] = new JsonObject { ["type"] = "string", ["format"] = "uuid", ["description"] = "Non-empty unique identifier." },
            ["HttpHostName"] = NullableString("Optional host name from which the resource can be retrieved."),
            ["HttpHostBasePath"] = NullableString("Optional service base path from which the resource can be retrieved."),
            ["HttpEndPoint"] = NullableString("Optional HTTP endpoint for this resource.")
        },
        ["required"] = new JsonArray("ID"),
        ["additionalProperties"] = false
    };

    private static JsonObject SuccessEnvelope(JsonObject data) => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject { ["status"] = SuccessStatus(), ["data"] = data },
        ["required"] = new JsonArray("status", "data"),
        ["additionalProperties"] = false
    };

    private static JsonObject SuccessStatus() => new()
    {
        ["type"] = "integer",
        ["minimum"] = 200,
        ["maximum"] = 299
    };

    private static JsonObject NullableString(string description) => new()
    {
        ["type"] = new JsonArray { "string", "null" },
        ["description"] = description
    };

    private static JsonObject NullableDateTime(string description) => new()
    {
        ["type"] = new JsonArray { "string", "null" },
        ["format"] = "date-time",
        ["description"] = description
    };

    private static JsonObject NullableUuid(string description) => new()
    {
        ["type"] = new JsonArray { "string", "null" },
        ["format"] = "uuid",
        ["description"] = description
    };

    public static bool TryParseGuid(JsonObject? arguments, string key, out Guid value, out JsonNode? error)
    {
        value = Guid.Empty;
        error = null;

        var node = arguments?[key];
        if (node is null)
        {
            error = McpToolResponses.CreateValidationError($"Argument '{key}' is required.");
            return false;
        }

        if (!Guid.TryParse(node.ToString(), out value))
        {
            error = McpToolResponses.CreateValidationError($"Argument '{key}' must be a valid UUID.");
            return false;
        }

        return true;
    }

    public static bool TryParseDouble(JsonObject? arguments, string key, out double value, out JsonNode? error)
    {
        value = 0d;
        error = null;

        var node = arguments?[key];
        if (node is null)
        {
            error = McpToolResponses.CreateValidationError($"Argument '{key}' is required.");
            return false;
        }

        try
        {
            value = node.GetValue<double>();
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException)
        {
            error = McpToolResponses.CreateValidationError($"Argument '{key}' must be a number.");
            return false;
        }

        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            error = McpToolResponses.CreateValidationError($"Argument '{key}' must be a finite number.");
            return false;
        }

        return true;
    }
}
