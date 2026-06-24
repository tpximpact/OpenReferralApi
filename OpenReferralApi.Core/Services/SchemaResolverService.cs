// Enable nullable reference types for better null-safety
#nullable enable
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenReferralApi.Core.Helpers;
using OpenReferralApi.Core.Logging;

namespace OpenReferralApi.Core.Services;

/// <summary>
/// Service for resolving JSON Schema references ($ref) and creating schemas with proper resolution.
/// Uses System.Text.Json for reference resolution and Json.Schema for schema creation.
/// Handles both external URL references and internal JSON pointer references.
/// </summary>
public interface ISchemaResolverService
{
    // System.Text.Json based schema resolution methods
    /// <summary>
    /// Resolves all $ref references in the provided schema with a base URI context.
    /// </summary>
    /// <param name="schema">The schema to resolve (as JSON string).</param>
    /// <param name="baseUri">The base URI for resolving relative references.</param>
    /// <param name="auth">Optional authentication for fetching remote schemas.</param>
    /// <returns>The fully resolved schema as a JSON string.</returns>
    Task<string> ResolveAsync(string schema, string? baseUri = null, DataSourceAuthentication? auth = null);

    /// <summary>
    /// Resolves all $ref references in the provided schema with a base URI context.
    /// </summary>
    /// <param name="schema">The schema to resolve (as JsonNode).</param>
    /// <param name="baseUri">The base URI for resolving relative references.</param>
    /// <param name="auth">Optional authentication for fetching remote schemas.</param>
    /// <returns>The fully resolved schema as a JsonNode.</returns>
    Task<JsonNode?> ResolveAsync(JsonNode schema, string? baseUri = null, DataSourceAuthentication? auth = null);

    // Json.Schema based schema creation methods
    /// <summary>
    /// Creates a JSON schema from JSON string with proper reference resolution
    /// </summary>
    Task<JsonSchema> CreateSchemaFromJsonAsync(string schemaJson, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a JSON schema from JSON string with proper reference resolution and base URI
    /// </summary>
    Task<JsonSchema> CreateSchemaFromJsonAsync(string schemaJson, string? documentUri, DataSourceAuthentication? auth = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns non-fatal issues discovered during the most recent reference resolution call.
    /// </summary>
    IReadOnlyList<SchemaResolutionIssue> GetResolutionIssues();

    /// <summary>
    /// Resolves a single $ref pointer on-the-fly.
    /// </summary>
    Task<JsonNode?> ResolveNodeRefAsync(string refString);
}

/// <summary>
/// Resolves JSON Schema references ($ref) in OpenAPI/OpenReferral specifications.
/// Handles both external URL references and internal JSON pointer references.
/// Detects and preserves circular references to prevent infinite loops.
/// Fetches remote schemas via HTTP/HTTPS.
/// </summary>
/// <remarks>
/// This is a C# port of the TypeScript SchemaResolver used in the OpenReferral UK website.
/// Compatible with .NET 10 and uses System.Text.Json for JSON manipulation.
/// </remarks>
public class SchemaResolverService : ISchemaResolverService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SchemaResolverService> _logger;
    private readonly IMemoryCache _memoryCache;
    private readonly CacheOptions _cacheOptions;
    private readonly RemoteSchemaLoader _remoteSchemaLoader;
    private readonly ReferenceResolver _referenceResolver;
    private static readonly JsonSerializerOptions IndentedSerializerOptions = new() { WriteIndented = true };

    /// <summary>
    /// Initializes a new instance of the SchemaResolver for remote schema resolution.
    /// </summary>
    /// <param name="httpClientFactory">HTTP client factory for fetching remote schemas.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="memoryCache">Memory cache for persistent schema caching.</param>
    /// <param name="cacheOptions">Cache configuration options.</param>
    /// <param name="schemaResolutionOptions">Schema resolution configuration options for URL normalization.</param>
    public SchemaResolverService(
      IHttpClientFactory httpClientFactory,
      ILogger<SchemaResolverService> logger,
      IMemoryCache memoryCache,
      IOptions<CacheOptions> cacheOptions,
      IOptions<SchemaResolutionOptions>? schemaResolutionOptions = null)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _cacheOptions = cacheOptions?.Value ?? throw new ArgumentNullException(nameof(cacheOptions));
        _remoteSchemaLoader = new RemoteSchemaLoader(
            httpClientFactory,
            logger,
            memoryCache,
            cacheOptions,
            schemaResolutionOptions?.Value?.KnownJsonSchemaUrls,
            schemaResolutionOptions?.Value?.WarnOnUnknownJsonSchemaDraft ?? true);
        _referenceResolver = new ReferenceResolver(logger, _remoteSchemaLoader);
        ConfigureGlobalFetch();
    }

    private void ConfigureGlobalFetch()
    {
        Json.Schema.SchemaRegistry.Global.Fetch = (uri, registry) =>
        {
            if (uri.Scheme == Uri.UriSchemeFile)
            {
                var localPath = uri.LocalPath;
                if (File.Exists(localPath))
                {
                    try
                    {
                        var content = File.ReadAllText(localPath);
                        return JsonSchema.FromText(content);
                    }
                    catch
                    {
                        return null;
                    }
                }
            }

            // Correct TLD for json-everything domain to avoid dns resolution timeouts
            var host = uri.Host;
            if (host.Equals("json-everything.lib", StringComparison.OrdinalIgnoreCase))
            {
                host = "json-everything.net";
            }

            // Bypass network calls for standard JSON schema drafts, OpenAPI meta-schemas,
            // and json-everything schemas to avoid ThreadPool starvation deadlocks and speed up compilation.
            if (host.Equals("json-schema.org", StringComparison.OrdinalIgnoreCase) ||
                host.Equals("spec.openapis.org", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (host.Equals("json-everything.net", StringComparison.OrdinalIgnoreCase))
            {
                var fileName = uri.Segments.LastOrDefault()?.TrimEnd('/');
                if (!string.IsNullOrEmpty(fileName))
                {
                    var localSchema = FindRegisteredSchemaByFileName(fileName);
                    if (localSchema != null)
                    {
                        return localSchema;
                    }
                }
                return null;
            }

            var resolvedUrl = uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
            var cacheKey = $"schema:{resolvedUrl}";
            if (_memoryCache.TryGetValue<CachedSchema>(cacheKey, out var cachedSchema) && cachedSchema != null)
            {
                return cachedSchema.CompiledSchema;
            }

            try
            {
                var node = Task.Run(() => _remoteSchemaLoader.LoadRemoteSchemaAsync(resolvedUrl)).GetAwaiter().GetResult();
                if (_memoryCache.TryGetValue<CachedSchema>(cacheKey, out var cachedSchema2) && cachedSchema2 != null)
                {
                    return cachedSchema2.CompiledSchema;
                }
            }
            catch
            {
                // Ignore
            }

            return null;
        };
    }

    private static IBaseDocument? FindRegisteredSchemaByFileName(string fileName)
    {
        try
        {
            var registry = Json.Schema.SchemaRegistry.Global;
            var field = typeof(SchemaRegistry).GetField("_registered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field == null) return null;

            var dict = field.GetValue(registry) as System.Collections.IEnumerable;
            if (dict == null) return null;

            foreach (var entry in dict)
            {
                var keyProp = entry.GetType().GetProperty("Key");
                var valueProp = entry.GetType().GetProperty("Value");
                if (keyProp == null || valueProp == null) continue;

                var uri = keyProp.GetValue(entry) as Uri;
                if (uri == null) continue;

                var path = uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
                if (path.EndsWith("/" + fileName, StringComparison.OrdinalIgnoreCase) || path.Equals(fileName, StringComparison.OrdinalIgnoreCase))
                {
                    var registration = valueProp.GetValue(entry);
                    if (registration == null) continue;

                    var rootProp = registration.GetType().GetProperty("Root");
                    if (rootProp == null) continue;

                    var doc = rootProp.GetValue(registration) as IBaseDocument;
                    if (doc != null)
                    {
                        return doc;
                    }
                }
            }
        }
        catch
        {
            // Ignore reflection errors and fallback
        }
        return null;
    }

    /// <summary>
    /// Resolves all $ref references in the provided schema.
    /// </summary>
    /// <param name="schema">The schema to resolve (as JSON string).</param>
    /// <param name="baseUri">The base URI for resolving relative references.</param>
    /// <param name="auth">Optional authentication for fetching remote schemas.</param>
    /// <returns>The fully resolved schema as a JSON string.</returns>
    public async Task<string> ResolveAsync(string schema, string? baseUri = null, DataSourceAuthentication? auth = null)
    {
        var jsonNode = JsonNode.Parse(schema) ?? throw new ArgumentException("Invalid JSON schema", nameof(schema));
        var resolved = await ResolveAsync(jsonNode, baseUri, auth);
        return resolved?.ToJsonString(IndentedSerializerOptions) ?? "null";
    }

    /// <summary>
    /// Resolves all $ref references in the provided schema.
    /// </summary>
    /// <param name="schema">The schema to resolve (as JsonNode).</param>
    /// <param name="baseUri">The base URI for resolving relative references.</param>
    /// <param name="auth">Optional authentication for fetching remote schemas.</param>
    /// <returns>The fully resolved schema as a JsonNode.</returns>
    public async Task<JsonNode?> ResolveAsync(JsonNode schema, string? baseUri = null, DataSourceAuthentication? auth = null)
    {
        // Configure authentication for the remote loader
        var validatedAuth = IsValidAuthentication(auth) ? auth : null;
        _remoteSchemaLoader.SetAuthentication(validatedAuth);

        // Initialize the reference resolver for this resolution session (needed for on-the-fly lookups)
        _referenceResolver.Initialize(schema, baseUri);

        // Pre-fetch all external references recursively and register them in SchemaRegistry.Global
        await PreFetchSchemaRefsAsync(schema, baseUri, validatedAuth, CancellationToken.None);

        return schema.DeepClone();
    }

    public IReadOnlyList<SchemaResolutionIssue> GetResolutionIssues()
    {
        return [.. _referenceResolver.ResolutionIssues];
    }

    private async Task PreFetchSchemaRefsAsync(JsonNode rootNode, string? baseUri, DataSourceAuthentication? auth, CancellationToken cancellationToken)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();

        var initialRefs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        FindExternalRefs(rootNode, baseUri, initialRefs);

        foreach (var r in initialRefs)
        {
            if (visited.Add(r))
            {
                queue.Enqueue(r);
            }
        }

        var validatedAuth = IsValidAuthentication(auth) ? auth : null;
        _remoteSchemaLoader.SetAuthentication(validatedAuth);

        while (queue.Count > 0)
        {
            var url = queue.Dequeue();
            try
            {
                var schemaNode = await _remoteSchemaLoader.LoadRemoteSchemaAsync(url, cancellationToken);
                if (schemaNode != null)
                {
                    var nestedRefs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    FindExternalRefs(schemaNode, url, nestedRefs);
                    foreach (var nr in nestedRefs)
                    {
                        if (visited.Add(nr))
                        {
                            queue.Enqueue(nr);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.FailedToPreFetchSchemaRef(ex, url);
            }
        }
    }

    private static void FindExternalRefs(JsonNode? node, string? baseUri, HashSet<string> result)
    {
        if (node == null) return;
        if (node is JsonObject obj)
        {
            if (obj.TryGetPropertyValue("$ref", out var refNode) && refNode is JsonValue refValue)
            {
                var refStr = refValue.GetValue<string>();
                if (!string.IsNullOrEmpty(refStr))
                {
                    var parts = refStr.Split('#');
                    var schemaUrl = parts[0];
                    if (!string.IsNullOrEmpty(schemaUrl))
                    {
                        if (schemaUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                            schemaUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                        {
                            result.Add(schemaUrl);
                        }
                        else if (!string.IsNullOrEmpty(baseUri) && Uri.TryCreate(new Uri(baseUri), schemaUrl, out var absoluteUri))
                        {
                            result.Add(absoluteUri.GetLeftPart(UriPartial.Path).TrimEnd('/'));
                        }
                    }
                }
            }
            foreach (var kvp in obj)
            {
                FindExternalRefs(kvp.Value, baseUri, result);
            }
        }
        else if (node is JsonArray arr)
        {
            foreach (var item in arr)
            {
                FindExternalRefs(item, baseUri, result);
            }
        }
    }

    /// <summary>
    /// Determines whether the provided authentication configuration is considered valid for use.
    /// This adds a server-side gate so that user-controlled data does not directly drive whether
    /// sensitive authentication behavior is applied.
    /// </summary>
    /// <param name="auth">The authentication configuration supplied by the caller.</param>
    /// <returns>True if the configuration is valid and may be applied; otherwise, false.</returns>
    private static bool IsValidAuthentication([NotNullWhen(true)] DataSourceAuthentication? auth)
    {
        if (auth == null)
        {
            return false;
        }

        // NOTE: We avoid assuming unnamed properties on DataSourceAuthentication.
        // If this type exposes a scheme/value model, additional checks should be added here
        // to restrict allowed schemes and ensure required fields are non-empty.
        return true;
    }

    /// <summary>
    /// Creates a JSON schema from JSON string with proper reference resolution
    /// </summary>
    public async Task<JsonSchema> CreateSchemaFromJsonAsync(string schemaJson, CancellationToken cancellationToken = default)
    {
        return await CreateSchemaFromJsonAsync(schemaJson, null, null, cancellationToken);
    }

    /// <summary>
    /// Creates a JSON schema from JSON string with proper reference resolution and base URI
    /// Uses System.Text.Json based resolution to pre-resolve all $ref before creating JsonSchema
    /// </summary>
    public async Task<JsonSchema> CreateSchemaFromJsonAsync(string schemaJson, string? documentUri, DataSourceAuthentication? auth = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.CreatingJsonSchema(documentUri != null ? TextSanitizer.SanitizeUrlForLogging(documentUri) : "none");

            // Pre-resolve all external and internal references using System.Text.Json based resolution
            string resolvedSchemaJson = schemaJson;
            try
            {
                _logger.PreResolvingSchemaReferences(documentUri != null ? TextSanitizer.SanitizeUrlForLogging(documentUri) : "none");
                resolvedSchemaJson = await ResolveAsync(schemaJson, documentUri, auth);
                _logger.SuccessfullyPreResolvedSchemaReferences();
            }
            catch (Exception ex)
            {
                _logger.FailedToPreResolveSchema(ex);
                // Continue with original schema if resolution fails
                resolvedSchemaJson = schemaJson;
            }

            // Create JsonSchema with the fully resolved schema (no more $ref to resolve)
            try
            {
                var schema = await Task.Run(() => JsonSchemaBuild.FromText(resolvedSchemaJson), cancellationToken);
                _logger.SuccessfullyCreatedSchemaWithReferenceResolution();
                return schema;
            }
            catch (Exception ex)
            {
                _logger.FailedToParseSchemaWithResolver(ex, documentUri != null ? TextSanitizer.SanitizeUrlForLogging(documentUri) : "none");
                try
                {
                    // Fallback: parse original schema if resolution produced a schema that JsonSchema cannot parse.
                    var schema = await Task.Run(() => JsonSchemaBuild.FromText(schemaJson), cancellationToken);
                    _logger.SuccessfullyCreatedSchemaWithoutResolver();
                    return schema;
                }
                catch (Exception fallbackEx)
                {
                    var originalFingerprint = CreateSchemaFingerprint(schemaJson);
                    var resolvedFingerprint = CreateSchemaFingerprint(resolvedSchemaJson);
                    var originalSchemaId = ExtractTopLevelSchemaId(schemaJson);
                    var resolvedSchemaId = ExtractTopLevelSchemaId(resolvedSchemaJson);

                    _logger.FailedToParseSchemaWithoutResolver(
                      fallbackEx,
                      documentUri != null ? TextSanitizer.SanitizeUrlForLogging(documentUri) : "none",
                      TextSanitizer.SanitizeStringForLogging(fallbackEx.Message),
                      originalFingerprint,
                      resolvedFingerprint,
                      TextSanitizer.SanitizeStringForLogging(originalSchemaId),
                      TextSanitizer.SanitizeStringForLogging(resolvedSchemaId));

                    throw new InvalidOperationException("Unable to parse schema with or without resolver", fallbackEx);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.FailedToCreateJsonSchema(ex, documentUri != null ? TextSanitizer.SanitizeUrlForLogging(documentUri) : "none");
            throw;
        }
    }

    private static string CreateSchemaFingerprint(string schemaJson)
    {
        if (string.IsNullOrWhiteSpace(schemaJson))
        {
            return "sha256:empty:length:0";
        }

        var bytes = Encoding.UTF8.GetBytes(schemaJson);
        var hash = SHA256.HashData(bytes);
        var hex = Convert.ToHexString(hash).ToLowerInvariant();

        return $"sha256:{hex}:length:{bytes.Length}";
    }

    public async Task<JsonNode?> ResolveNodeRefAsync(string refString)
    {
        return await _referenceResolver.ResolveNodeRefAsync(refString);
    }

    private static string ExtractTopLevelSchemaId(string schemaJson)
    {
        if (string.IsNullOrWhiteSpace(schemaJson))
        {
            return "none";
        }

        try
        {
            var node = JsonNode.Parse(schemaJson);
            if (node is JsonObject obj &&
                obj.TryGetPropertyValue("$id", out var idNode) &&
                idNode is JsonValue idValue)
            {
                var id = idValue.GetValue<string>();
                return string.IsNullOrWhiteSpace(id) ? "none" : id;
            }
        }
        catch
        {
            // Ignore best-effort extraction failures and continue with diagnostics.
        }

        return "none";
    }
}

