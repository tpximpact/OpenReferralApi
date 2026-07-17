using System;
using System.Threading;
using Json.Schema;
using Json.Schema.OpenApi;

namespace OpenReferralApi.Core.Helpers;

internal static class JsonSchemaBuild
{
    private static readonly Lazy<bool> OpenApiMetaSchemaRegistration = new(() =>
    {
        try
        {
            Json.Schema.OpenApi.MetaSchemas.Register();
        }
        catch (ArgumentException ex) when (ex.Message.Contains("same key has already been added", StringComparison.OrdinalIgnoreCase))
        {
            // Registration is global; if another startup path got there first, continue.
        }

        try
        {
            RegisterIfMissing(Json.Schema.MetaSchemas.Draft202012Id, Json.Schema.MetaSchemas.Draft202012);
            RegisterIfMissing(Json.Schema.MetaSchemas.Core202012Id, Json.Schema.MetaSchemas.Core202012);
            RegisterIfMissing(Json.Schema.MetaSchemas.Applicator202012Id, Json.Schema.MetaSchemas.Applicator202012);
            RegisterIfMissing(Json.Schema.MetaSchemas.Unevaluated202012Id, Json.Schema.MetaSchemas.Unevaluated202012);
            RegisterIfMissing(Json.Schema.MetaSchemas.Validation202012Id, Json.Schema.MetaSchemas.Validation202012);
            RegisterIfMissing(Json.Schema.MetaSchemas.Metadata202012Id, Json.Schema.MetaSchemas.Metadata202012);
            RegisterIfMissing(Json.Schema.MetaSchemas.FormatAnnotation202012Id, Json.Schema.MetaSchemas.FormatAnnotation202012);
            RegisterIfMissing(Json.Schema.MetaSchemas.Content202012Id, Json.Schema.MetaSchemas.Content202012);
        }
        catch
        {
            // Ignore
        }

        return true;
    }, LazyThreadSafetyMode.ExecutionAndPublication);

    public static JsonSchema FromText(string schemaJson)
    {
        EnsureInitialized();

        // Check if the schema is already registered to prevent duplicate key exceptions
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(schemaJson);
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("$id", out var idProp) &&
                idProp.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var id = idProp.GetString();
                if (!string.IsNullOrEmpty(id) && Uri.TryCreate(id, UriKind.Absolute, out var uri))
                {
                    if (SchemaRegistry.Global.Get(uri) is JsonSchema registered)
                    {
                        return registered;
                    }
                }
            }
        }
        catch
        {
            // Ignore parsing/lookup errors and fall back to compiling the schema.
        }

        var options = new BuildOptions
        {
            Dialect = Json.Schema.Dialect.Draft202012
        };
        return JsonSchema.FromText(schemaJson, options);
    }

    internal static void EnsureInitialized()
    {
        _ = OpenApiMetaSchemaRegistration.Value;
    }

    private static void RegisterIfMissing(Uri uri, JsonSchema schema)
    {
        if (SchemaRegistry.Global.Get(uri) == null)
        {
            try
            {
                SchemaRegistry.Global.Register(uri, schema);
            }
            catch (ArgumentException)
            {
                // Already registered concurrently.
            }
        }
    }
}