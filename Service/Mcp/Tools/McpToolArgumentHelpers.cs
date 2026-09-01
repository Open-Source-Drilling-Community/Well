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
            ["well"] = CreateWellObjectSchema(includeId ? WellSchemaKind.UpdateInput : WellSchemaKind.CreateInput)
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
            properties["expectedModifiedUtc"] = new JsonObject
            {
                ["type"] = "string",
                ["format"] = "date-time",
                ["description"] = "LastModificationDate returned by the latest read of this well. The update is rejected if the stored revision has changed."
            };
            required.Add("expectedModifiedUtc");
        }

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required,
            ["additionalProperties"] = false
        };
    }

    public static JsonObject CreateWellResourceSchema() => CreateWellObjectSchema(WellSchemaKind.Response);

    public static JsonObject CreateWellDeleteSchema() => CreateTimestampedIdSchema("id", "Identifier of the Well to delete.");

    public static JsonObject CreateWellDetailsMutationSchema() => CreateWellSubresourceMutationSchema("details", new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["Name"] = NullableString("Complete replacement value for the Well name."),
            ["Description"] = NullableString("Complete replacement value for the Well description.")
        },
        ["required"] = new JsonArray("Name", "Description"),
        ["additionalProperties"] = false
    });

    public static JsonObject CreateWellLocationMutationSchema() => CreateWellSubresourceMutationSchema("location", new JsonObject
    {
        ["type"] = "object",
        ["description"] = "External Cluster/Slot references. UUID existence is owned by the Cluster service and is not synchronously validated by Well.",
        ["properties"] = new JsonObject
        {
            ["ClusterID"] = NullableUuid("External Cluster UUID, or null when unassigned."),
            ["SlotID"] = NullableUuid("External Slot UUID, or null when unassigned. A non-null SlotID requires ClusterID."),
            ["IsSingleWell"] = new JsonObject { ["type"] = "boolean" }
        },
        ["required"] = new JsonArray("ClusterID", "SlotID", "IsSingleWell"),
        ["additionalProperties"] = false
    });

    private static JsonObject CreateWellSubresourceMutationSchema(string bodyName, JsonObject body)
    {
        JsonObject schema = CreateTimestampedIdSchema("id", "Identifier of the Well to mutate.");
        JsonObject properties = (JsonObject)schema["properties"]!;
        properties[bodyName] = body;
        ((JsonArray)schema["required"]!).Add(bodyName);
        return schema;
    }

    private static JsonObject CreateTimestampedIdSchema(string idName, string description) => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            [idName] = new JsonObject { ["type"] = "string", ["format"] = "uuid", ["description"] = description },
            ["expectedModifiedUtc"] = new JsonObject
            {
                ["type"] = "string", ["format"] = "date-time",
                ["description"] = "The LastModificationDate from the caller's latest Well read."
            }
        },
        ["required"] = new JsonArray(idName, "expectedModifiedUtc"),
        ["additionalProperties"] = false
    };

    public static JsonObject CreateWellIdentitySchema(bool includeId = false) =>
        WrapCatalogBody("wellIdentity", CreateIdentityDefinitionSchema(), includeId, "wellIdentity.MetaInfo.ID");

    public static JsonObject CreateWellIdentityResourceSchema() => CreateIdentityDefinitionSchema();

    public static JsonObject CreateWellFeatureCategorySchema(bool includeId = false) =>
        WrapCatalogBody("wellFeatureCategory", CreateFeatureCategorySchema(), includeId, "wellFeatureCategory.MetaInfo.ID");

    public static JsonObject CreateWellFeatureCategoryResourceSchema() => CreateFeatureCategorySchema();

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

    public static JsonObject CreateWellOutputSchema() => SuccessEnvelope(CreateWellObjectSchema(WellSchemaKind.Response));

    public static JsonObject CreateWellListOutputSchema() => SuccessEnvelope(new JsonObject
    {
        ["type"] = "array",
        ["items"] = CreateWellObjectSchema(WellSchemaKind.Response)
    });

    public static JsonObject CreateWellSearchSchema() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["offset"] = new JsonObject { ["type"] = "integer", ["minimum"] = 0, ["default"] = 0 },
            ["limit"] = new JsonObject { ["type"] = "integer", ["minimum"] = 1, ["maximum"] = 200, ["default"] = 50 },
            ["name"] = new JsonObject { ["type"] = "string", ["maxLength"] = 200 },
            ["clusterId"] = new JsonObject { ["type"] = "string", ["format"] = "uuid" },
            ["slotId"] = new JsonObject { ["type"] = "string", ["format"] = "uuid" },
            ["identityId"] = new JsonObject { ["type"] = "string", ["format"] = "uuid" },
            ["identityValue"] = new JsonObject { ["type"] = "string", ["maxLength"] = 500 },
            ["featureCategoryId"] = new JsonObject { ["type"] = "string", ["format"] = "uuid" },
            ["featureOptionId"] = new JsonObject { ["type"] = "string", ["format"] = "uuid" },
            ["modifiedFromUtc"] = new JsonObject { ["type"] = "string", ["format"] = "date-time" },
            ["modifiedToUtc"] = new JsonObject { ["type"] = "string", ["format"] = "date-time" }
        },
        ["additionalProperties"] = false
    };

    public static JsonObject CreateWellSearchOutputSchema() => SuccessEnvelope(new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["Items"] = new JsonObject { ["type"] = "array", ["items"] = CreateWellObjectSchema(WellSchemaKind.Response) },
            ["Total"] = NonNegativeInteger(),
            ["Offset"] = NonNegativeInteger(),
            ["Limit"] = new JsonObject { ["type"] = "integer", ["minimum"] = 1, ["maximum"] = 200 }
        },
        ["required"] = new JsonArray("Items", "Total", "Offset", "Limit"),
        ["additionalProperties"] = false
    });

    public static JsonObject CreateWellExternalReferenceValidationOutputSchema() =>
        SuccessEnvelope(CreateWellExternalReferenceValidationSchema());

    public static JsonObject CreateWellExternalReferenceAuditSchema() => WrapRequest(new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["Scope"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("All", "Selected") },
            ["WellIDs"] = new JsonObject
            {
                ["type"] = new JsonArray("array", "null"), ["uniqueItems"] = true,
                ["items"] = new JsonObject { ["type"] = "string", ["format"] = "uuid" }
            },
            ["Offset"] = new JsonObject { ["type"] = "integer", ["minimum"] = 0, ["default"] = 0 },
            ["Limit"] = new JsonObject { ["type"] = "integer", ["minimum"] = 1, ["maximum"] = 100, ["default"] = 100 }
        },
        ["required"] = new JsonArray("Scope"),
        ["additionalProperties"] = false
    });

    public static JsonObject CreateWellExternalReferenceAuditOutputSchema() => SuccessEnvelope(new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["CheckedAtUtc"] = DateTimeSchema("Timestamp at which this audit page was checked."),
            ["Total"] = NonNegativeInteger(), ["Offset"] = NonNegativeInteger(),
            ["Limit"] = new JsonObject { ["type"] = "integer", ["minimum"] = 1, ["maximum"] = 100 },
            ["ValidCount"] = NonNegativeInteger(), ["InvalidCount"] = NonNegativeInteger(),
            ["UnavailableCount"] = NonNegativeInteger(),
            ["Items"] = new JsonObject { ["type"] = "array", ["items"] = CreateWellExternalReferenceValidationSchema() }
        },
        ["required"] = new JsonArray("CheckedAtUtc", "Total", "Offset", "Limit", "ValidCount", "InvalidCount", "UnavailableCount", "Items"),
        ["additionalProperties"] = false
    });

    private static JsonObject CreateWellExternalReferenceValidationSchema() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["WellID"] = new JsonObject { ["type"] = "string", ["format"] = "uuid" },
            ["ClusterID"] = NullableUuid("Cluster UUID recorded by the Well, or null."),
            ["SlotID"] = NullableUuid("Slot UUID recorded by the Well, or null."),
            ["ClusterExists"] = new JsonObject { ["type"] = new JsonArray("boolean", "null") },
            ["SlotBelongsToCluster"] = new JsonObject { ["type"] = new JsonArray("boolean", "null") },
            ["Status"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("Valid", "Invalid", "Unavailable") },
            ["CheckedAtUtc"] = DateTimeSchema("Timestamp at which the external references were checked."),
            ["Issues"] = new JsonObject
            {
                ["type"] = "array", ["items"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["Property"] = new JsonObject { ["type"] = "string" },
                        ["Code"] = new JsonObject { ["type"] = "string" },
                        ["Message"] = new JsonObject { ["type"] = "string" }
                    },
                    ["required"] = new JsonArray("Property", "Code", "Message"), ["additionalProperties"] = false
                }
            }
        },
        ["required"] = new JsonArray("WellID", "ClusterID", "SlotID", "ClusterExists", "SlotBelongsToCluster", "Status", "CheckedAtUtc", "Issues"),
        ["additionalProperties"] = false
    };

    public static JsonObject CreateIdentityAssignmentMutationSchema(bool includeAssignmentId, bool includeBody) =>
        CreateAssignmentMutationSchema(CreateIdentityAssignmentSchema(), includeAssignmentId, includeBody);

    public static JsonObject CreateFeatureAssignmentMutationSchema(bool includeAssignmentId, bool includeBody) =>
        CreateAssignmentMutationSchema(CreateFeatureAssignmentSchema(), includeAssignmentId, includeBody);

    private static JsonObject CreateAssignmentMutationSchema(JsonObject assignmentSchema, bool includeAssignmentId, bool includeBody)
    {
        JsonObject properties = new()
        {
            ["wellId"] = new JsonObject { ["type"] = "string", ["format"] = "uuid" },
            ["expectedModifiedUtc"] = new JsonObject { ["type"] = "string", ["format"] = "date-time" }
        };
        JsonArray required = new("wellId", "expectedModifiedUtc");
        if (includeAssignmentId)
        {
            properties["assignmentId"] = new JsonObject { ["type"] = "string", ["format"] = "uuid" };
            required.Add("assignmentId");
        }
        if (includeBody)
        {
            properties["assignment"] = assignmentSchema;
            required.Add("assignment");
        }
        return new JsonObject
        {
            ["type"] = "object", ["properties"] = properties, ["required"] = required,
            ["additionalProperties"] = false
        };
    }

    public static JsonObject CreateResourceOutputSchema(JsonObject resource) => SuccessEnvelope(resource);

    public static JsonObject CreateResourceListOutputSchema(JsonObject resource) => SuccessEnvelope(new JsonObject
    {
        ["type"] = "array",
        ["items"] = resource
    });

    private enum WellSchemaKind
    {
        CreateInput,
        UpdateInput,
        Response,
        BatchDocument
    }

    private static JsonObject CreateWellObjectSchema(WellSchemaKind kind)
    {
        bool response = kind == WellSchemaKind.Response;
        bool mutationInput = kind is WellSchemaKind.CreateInput or WellSchemaKind.UpdateInput;
        JsonArray required = new("MetaInfo");
        if (response)
        {
            required.Add("CreationDate");
            required.Add("LastModificationDate");
        }

        return new JsonObject
        {
            ["type"] = "object",
            ["description"] = mutationInput
                ? "Complete Well write model. MetaInfo.ID must be a non-empty caller-generated UUID. CreationDate and LastModificationDate are server-owned; supplied values are ignored."
                : "Complete Well resource. MetaInfo.ID is a non-empty UUID and the timestamps are server-owned revisions.",
            ["properties"] = new JsonObject
            {
                ["MetaInfo"] = CreateMetaInfoSchema(),
                ["Name"] = NullableString("Human-readable well name."),
                ["Description"] = NullableString("Human-readable description of the well."),
                ["CreationDate"] = response
                    ? DateTimeSchema("Server-owned creation timestamp.")
                    : NullableDateTime(mutationInput ? "Server-owned; a supplied value is ignored." : "Creation timestamp retained for portable legacy backup documents."),
                ["LastModificationDate"] = response
                    ? DateTimeSchema("Server-owned optimistic-concurrency revision timestamp.")
                    : NullableDateTime(mutationInput ? "Server-owned; a supplied value is ignored." : "Modification timestamp retained for portable legacy backup documents."),
                ["SlotID"] = NullableUuid("External Slot-service identifier. Its existence is not synchronously validated by Well."),
                ["ClusterID"] = NullableUuid("External Cluster-service identifier. Its existence is not synchronously validated by Well."),
                ["IsSingleWell"] = new JsonObject
                {
                    ["type"] = "boolean",
                    ["description"] = "True when the cluster is only a proxy for a standalone well.",
                    ["default"] = false
                },
                ["WellIdentityAssignments"] = NullableArray(CreateIdentityAssignmentSchema()),
                ["WellFeatureAssignments"] = NullableArray(CreateFeatureAssignmentSchema())
            },
            ["required"] = required,
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

    public static JsonObject CreateWellBatchExportSchema() => WrapRequest(new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["Scope"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("All", "Selected") },
            ["WellIDs"] = new JsonObject
            {
                ["type"] = new JsonArray("array", "null"), ["uniqueItems"] = true,
                ["items"] = new JsonObject { ["type"] = "string", ["format"] = "uuid" }
            }
        },
        ["required"] = new JsonArray("Scope"),
        ["additionalProperties"] = false
    });

    public static JsonObject CreateWellBatchRestoreSchema() => WrapRequest(new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["ConflictPolicy"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("FailIfExists", "ReplaceExisting") },
            ["CatalogPolicy"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("MapExisting", "MapOrCreateMissing") },
            ["Document"] = CreateBatchDocumentSchema(1)
        },
        ["required"] = new JsonArray("ConflictPolicy", "CatalogPolicy", "Document"),
        ["additionalProperties"] = false
    });

    public static JsonObject CreateWellBatchExportOutputSchema() => SuccessEnvelope(CreateBatchDocumentSchema(0));

    public static JsonObject CreateWellBatchRestoreOutputSchema() => SuccessEnvelope(new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["RestoredAtUtc"] = new JsonObject { ["type"] = "string", ["format"] = "date-time" },
            ["CreatedCount"] = NonNegativeInteger(), ["ReplacedCount"] = NonNegativeInteger(),
            ["CreatedCatalogDefinitionCount"] = NonNegativeInteger(), ["CreatedCatalogOptionCount"] = NonNegativeInteger(),
            ["CatalogMappings"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["Catalog"] = new JsonObject { ["type"] = "string", ["minLength"] = 1 },
                        ["Name"] = new JsonObject { ["type"] = "string" },
                        ["SourceID"] = new JsonObject { ["type"] = "string", ["format"] = "uuid" },
                        ["LocalID"] = new JsonObject { ["type"] = "string", ["format"] = "uuid" },
                        ["Resolution"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("exact_uuid", "normalized_name", "created") }
                    },
                    ["required"] = new JsonArray("Catalog", "Name", "SourceID", "LocalID", "Resolution"),
                    ["additionalProperties"] = false
                }
            },
            ["WellIDs"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string", ["format"] = "uuid" } }
        },
        ["required"] = new JsonArray("RestoredAtUtc", "CreatedCount", "ReplacedCount", "CreatedCatalogDefinitionCount", "CreatedCatalogOptionCount", "CatalogMappings", "WellIDs"),
        ["additionalProperties"] = false
    });

    private static JsonObject CreateBatchDocumentSchema(int minimumWells) => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["FormatIdentifier"] = new JsonObject { ["type"] = "string", ["const"] = "OSDC.Drilling.Well.BatchExport" },
            ["SchemaVersion"] = new JsonObject { ["type"] = "integer", ["const"] = 1 },
            ["ExportedAtUtc"] = new JsonObject { ["type"] = "string", ["format"] = "date-time" },
            ["CatalogDependencies"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["Identities"] = new JsonObject { ["type"] = "array", ["items"] = CreateIdentityDefinitionSchema() },
                    ["FeatureCategories"] = new JsonObject { ["type"] = "array", ["items"] = CreateFeatureCategorySchema() }
                },
                ["required"] = new JsonArray("Identities", "FeatureCategories"), ["additionalProperties"] = false
            },
            ["Wells"] = new JsonObject { ["type"] = "array", ["minItems"] = minimumWells, ["items"] = CreateWellObjectSchema(WellSchemaKind.BatchDocument) }
        },
        ["required"] = new JsonArray("FormatIdentifier", "SchemaVersion", "ExportedAtUtc", "CatalogDependencies", "Wells"),
        ["additionalProperties"] = false
    };

    private static JsonObject WrapRequest(JsonObject request) => new()
    {
        ["type"] = "object", ["properties"] = new JsonObject { ["request"] = request },
        ["required"] = new JsonArray("request"), ["additionalProperties"] = false
    };

    private static JsonObject CreateIdentityDefinitionSchema() => new()
    {
        ["type"] = "object", ["properties"] = new JsonObject
        {
            ["MetaInfo"] = CreateMetaInfoSchema(), ["Name"] = RequiredName("Identity name."),
            ["CreationDate"] = NullableDateTime("Creation timestamp."), ["LastModificationDate"] = NullableDateTime("Modification timestamp.")
        },
        ["required"] = new JsonArray("MetaInfo", "Name"), ["additionalProperties"] = false
    };

    private static JsonObject CreateFeatureCategorySchema() => new()
    {
        ["type"] = "object", ["properties"] = new JsonObject
        {
            ["MetaInfo"] = CreateMetaInfoSchema(), ["Name"] = RequiredName("Category name."),
            ["IsExclusive"] = new JsonObject { ["type"] = "boolean" }, ["HasValidityPeriod"] = new JsonObject { ["type"] = "boolean" },
            ["Options"] = NullableArray(new JsonObject { ["type"] = "object", ["properties"] = new JsonObject { ["ID"] = new JsonObject { ["type"] = "string", ["format"] = "uuid" }, ["Name"] = RequiredName("Option name.") }, ["required"] = new JsonArray("ID", "Name"), ["additionalProperties"] = false }),
            ["CreationDate"] = NullableDateTime("Creation timestamp."), ["LastModificationDate"] = NullableDateTime("Modification timestamp.")
        },
        ["required"] = new JsonArray("MetaInfo", "Name", "IsExclusive", "HasValidityPeriod", "Options"), ["additionalProperties"] = false
    };

    private static JsonObject WrapCatalogBody(string key, JsonObject body, bool includeId, string idPath)
    {
        JsonObject properties = new() { [key] = body };
        JsonArray required = new(key);
        if (includeId)
        {
            properties["id"] = new JsonObject
            {
                ["type"] = "string", ["format"] = "uuid",
                ["description"] = $"Identifier of the stored definition to update. It must equal {idPath}."
            };
            properties["expectedModifiedUtc"] = new JsonObject
            {
                ["type"] = "string", ["format"] = "date-time",
                ["description"] = "Optimistic-concurrency token. It must equal the latest server LastModificationDate."
            };
            required.Add("id");
            required.Add("expectedModifiedUtc");
        }
        return new JsonObject
        {
            ["type"] = "object", ["properties"] = properties, ["required"] = required,
            ["additionalProperties"] = false
        };
    }

    private static JsonObject CreateIdentityAssignmentSchema() => new()
    {
        ["type"] = "object", ["properties"] = new JsonObject
        {
            ["ID"] = new JsonObject { ["type"] = "string", ["format"] = "uuid" },
            ["IdentityID"] = new JsonObject { ["type"] = "string", ["format"] = "uuid", ["description"] = "Referenced identity." },
            ["Value"] = new JsonObject { ["type"] = "string", ["minLength"] = 1, ["description"] = "Well-specific identity value." }
        }, ["required"] = new JsonArray("ID", "IdentityID", "Value"), ["additionalProperties"] = false
    };

    private static JsonObject CreateFeatureAssignmentSchema() => new()
    {
        ["type"] = "object", ["properties"] = new JsonObject
        {
            ["ID"] = new JsonObject { ["type"] = "string", ["format"] = "uuid" },
            ["FeatureCategoryID"] = new JsonObject { ["type"] = "string", ["format"] = "uuid", ["description"] = "Referenced feature category." },
            ["FeatureOptionID"] = new JsonObject { ["type"] = "string", ["format"] = "uuid", ["description"] = "Referenced feature option." },
            ["FromDate"] = NullableDateTime("Validity start."), ["ToDate"] = NullableDateTime("Validity end.")
        }, ["required"] = new JsonArray("ID", "FeatureCategoryID", "FeatureOptionID"), ["additionalProperties"] = false
    };

    private static JsonObject NullableArray(JsonObject item) => new() { ["type"] = new JsonArray("array", "null"), ["items"] = item };
    private static JsonObject NonNegativeInteger() => new() { ["type"] = "integer", ["minimum"] = 0 };

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

    private static JsonObject RequiredName(string description) => new()
    {
        ["type"] = "string",
        ["minLength"] = 1,
        ["description"] = description
    };

    private static JsonObject NullableDateTime(string description) => new()
    {
        ["type"] = new JsonArray { "string", "null" },
        ["format"] = "date-time",
        ["description"] = description
    };

    private static JsonObject DateTimeSchema(string description) => new()
    {
        ["type"] = "string",
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

        if (!Guid.TryParse(node.ToString(), out value) || value == Guid.Empty)
        {
            error = McpToolResponses.CreateValidationError($"Argument '{key}' must be a valid non-empty UUID.");
            return false;
        }

        return true;
    }

    public static bool TryParseDateTimeOffset(JsonObject? arguments, string key, out DateTimeOffset value, out JsonNode? error)
    {
        value = default;
        error = null;
        JsonNode? node = arguments?[key];
        if (node == null)
        {
            error = McpToolResponses.CreateValidationError($"Argument '{key}' is required.");
            return false;
        }
        if (!DateTimeOffset.TryParse(node.ToString(), System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind, out value) || value == default)
        {
            error = McpToolResponses.CreateValidationError($"Argument '{key}' must be a valid non-default ISO 8601 timestamp.");
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
