// Enable nullable reference types for better null-safety
#nullable enable
using System.Collections.Concurrent;
using System.Net.Http.Headers;
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
/// Structured record to cache compiled schemas and their parsed representation.
/// </summary>
public sealed record CachedSchema(
    JsonSchema CompiledSchema,
    JsonNode JsonRepresentation,
    string RawJson,
    int SizeInBytes
);

/// <summary>
/// Internal helper class for loading remote JSON schemas with caching and authentication support.
/// </summary>
public class RemoteSchemaLoader
{
    private readonly HashSet<string> _knownJsonSchemaUrls;
    private readonly HashSet<string> _unknownDraftWarnings = new(StringComparer.OrdinalIgnoreCase);
    private readonly bool _warnOnUnknownJsonSchemaDraft;
    private readonly ConcurrentDictionary<string, Task<JsonNode?>> _activeLoads = new(StringComparer.OrdinalIgnoreCase);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger _logger;
    private readonly IMemoryCache _memoryCache;
    private readonly CacheOptions _cacheOptions;
    private IAuthenticationConfig? _auth;

    public RemoteSchemaLoader(
        IHttpClientFactory httpClientFactory,
        ILogger logger,
        IMemoryCache memoryCache,
        IOptions<CacheOptions> cacheOptions,
        IEnumerable<string>? knownJsonSchemaUrls = null,
        bool warnOnUnknownJsonSchemaDraft = true)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _cacheOptions = cacheOptions?.Value ?? throw new ArgumentNullException(nameof(cacheOptions));
        _warnOnUnknownJsonSchemaDraft = warnOnUnknownJsonSchemaDraft;
        _knownJsonSchemaUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var configuredUrls = knownJsonSchemaUrls ?? SchemaResolutionOptionsDefaults.KnownJsonSchemaUrls;
        foreach (var url in configuredUrls)
        {
            var normalized = NormalizeAbsoluteUrl(url);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                _ = _knownJsonSchemaUrls.Add(normalized);
            }
        }

        // Trigger initialization of standard meta-schemas to populate SchemaRegistry.Global
        try
        {
            _ = OpenReferralApi.Core.Helpers.JsonSchemaBuild.FromText("{}");
        }
        catch
        {
            // Ignore initialization errors
        }
    }

    /// <summary>
    /// Sets the authentication configuration for this loader instance.
    /// </summary>
    public void SetAuthentication(IAuthenticationConfig? auth)
    {
        _auth = auth;
    }

    public async Task<JsonNode?> LoadRemoteSchemaAsync(string schemaUrl, CancellationToken cancellationToken = default)
    {
        if (schemaUrl.Contains("json-everything.lib", StringComparison.OrdinalIgnoreCase))
        {
            schemaUrl = schemaUrl.Replace("json-everything.lib", "json-everything.net", StringComparison.OrdinalIgnoreCase);
        }
        var resolvedUrl = NormalizeKnownSchemaUrl(schemaUrl) ?? schemaUrl;

        // 1. Check SchemaRegistry.Global first for known public meta-schemas
        if (Uri.TryCreate(resolvedUrl, UriKind.Absolute, out var schemaUri))
        {
            var isPublicMetaSchema = schemaUri.Host.Equals("json-schema.org", StringComparison.OrdinalIgnoreCase) ||
                                     schemaUri.Host.Equals("spec.openapis.org", StringComparison.OrdinalIgnoreCase) ||
                                     schemaUri.Host.Equals("json-everything.net", StringComparison.OrdinalIgnoreCase);

            if (isPublicMetaSchema)
            {
                var registered = Json.Schema.SchemaRegistry.Global.Get(schemaUri);
                if (registered != null)
                {
                    var cacheKey = GenerateCacheKey(resolvedUrl);
                    if (_cacheOptions.Enabled && _memoryCache.TryGetValue<CachedSchema>(cacheKey, out var cachedSchema) && cachedSchema != null)
                    {
                        return cachedSchema.JsonRepresentation.DeepClone();
                    }

                    try
                    {
                        var serialized = JsonSerializer.SerializeToNode(registered);
                        if (serialized != null)
                        {
                            return serialized;
                        }
                    }
                    catch
                    {
                        // Fallback
                    }
                    return new JsonObject();
                }
            }
        }

        // 2. Check persistent cache first if caching is enabled
        if (_cacheOptions.Enabled)
        {
            var cacheKey = GenerateCacheKey(resolvedUrl);
            if (_memoryCache.TryGetValue<CachedSchema>(cacheKey, out var cachedSchema) && cachedSchema != null)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.RetrievedSchemaFromCache(TextSanitizer.SanitizeUrlForLogging(resolvedUrl));
                }
                
                try
                {
                    if (Uri.TryCreate(resolvedUrl, UriKind.Absolute, out var cacheUri) &&
                        Json.Schema.SchemaRegistry.Global.Get(cacheUri) == null)
                    {
                        Json.Schema.SchemaRegistry.Global.Register(cacheUri, cachedSchema.CompiledSchema);
                    }
                }
                catch { /* Ignore */ }

                return cachedSchema.JsonRepresentation.DeepClone();
            }
        }

        // 3. Gate concurrent loads using _activeLoads
        Task<JsonNode?>? loadTask;
        bool isNewTask = false;
        
        lock (_activeLoads)
        {
            if (!_activeLoads.TryGetValue(resolvedUrl, out loadTask))
            {
                loadTask = LoadRemoteSchemaInternalAsync(resolvedUrl, cancellationToken);
                _activeLoads[resolvedUrl] = loadTask;
                isNewTask = true;
            }
        }

        try
        {
            return await loadTask;
        }
        finally
        {
            if (isNewTask)
            {
                lock (_activeLoads)
                {
                    _activeLoads.TryRemove(resolvedUrl, out _);
                }
            }
        }
    }

    private async Task<JsonNode?> LoadRemoteSchemaInternalAsync(string resolvedUrl, CancellationToken cancellationToken)
    {
        try
        {
            // Validate URL before making HTTP request to prevent SSRF attacks
            if (!Uri.TryCreate(resolvedUrl, UriKind.Absolute, out var schemaUri) ||
                (schemaUri.Scheme != Uri.UriSchemeHttp && schemaUri.Scheme != Uri.UriSchemeHttps))
            {
                throw new ArgumentException($"Invalid schema URL: Only HTTP and HTTPS URLs are allowed", nameof(resolvedUrl));
            }

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.FetchingRemoteSchema(TextSanitizer.SanitizeUrlForLogging(resolvedUrl));
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, resolvedUrl);

            // Apply authentication only if the configuration is considered valid
            if (_auth != null && IsValidAuthentication(_auth))
            {
                ApplyAuthentication(request, _auth);
            }

            var httpClient = _httpClientFactory.CreateClient("OpenApiValidationService");
            string content;
            try
            {
                using var response = await httpClient.SendAsync(request, cancellationToken);
                _ = response.EnsureSuccessStatusCode();
                content = await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                RemoteSchemaLoaderLog.ConnectionFailureFetchingRemoteSchema(_logger, ex, TextSanitizer.SanitizeUrlForLogging(resolvedUrl), ex.Message);
                return null;
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                RemoteSchemaLoaderLog.ConnectionFailureFetchingRemoteSchema(_logger, ex, TextSanitizer.SanitizeUrlForLogging(resolvedUrl), "Request timed out.");
                return null;
            }

            var jsonNode = JsonNode.Parse(content) ?? throw new InvalidOperationException("Fetched content is not valid JSON.");
            var cacheKey = GenerateCacheKey(resolvedUrl);
            var cacheEntryOptions = new MemoryCacheEntryOptions
            {
                Size = content.Length,
                Priority = CacheItemPriority.Normal
            };

            // Configure expiration
            if (_cacheOptions.ExpirationMinutes > 0)
            {
                if (_cacheOptions.UseSlidingExpiration)
                {
                    cacheEntryOptions.SlidingExpiration = TimeSpan.FromMinutes(_cacheOptions.SlidingExpirationMinutes);
                    cacheEntryOptions.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_cacheOptions.ExpirationMinutes);
                }
                else
                {
                    cacheEntryOptions.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_cacheOptions.ExpirationMinutes);
                }
            }

            // Put placeholder in cache to prevent circular recursion/deadlocks during compilation
            if (_cacheOptions.Enabled)
            {
                var placeholder = new CachedSchema(new JsonSchemaBuilder().Build(), jsonNode, content, content.Length);
                _memoryCache.Set(cacheKey, placeholder, cacheEntryOptions);
            }

            // Double check SchemaRegistry.Global right before compiling to avoid duplicate key exceptions
            if (Json.Schema.SchemaRegistry.Global.Get(schemaUri) != null)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Schema for {Url} was registered concurrently in SchemaRegistry.Global.", resolvedUrl);
                }
                return jsonNode.DeepClone();
            }

            var schema = OpenReferralApi.Core.Helpers.JsonSchemaBuild.FromText(content);

            // Store in persistent cache if caching is enabled
            if (_cacheOptions.Enabled)
            {
                var cachedSchema = new CachedSchema(schema, jsonNode, content, content.Length);
                _ = _memoryCache.Set(cacheKey, cachedSchema, cacheEntryOptions);
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.CachedSchema(TextSanitizer.SanitizeUrlForLogging(resolvedUrl), _cacheOptions.ExpirationMinutes);
                }
            }

            try
            {
                if (Json.Schema.SchemaRegistry.Global.Get(schemaUri) == null)
                {
                    Json.Schema.SchemaRegistry.Global.Register(schemaUri, schema);
                }
            }
            catch { /* Ignore registration errors if it's not a valid schema (e.g. partial component) */ }

            return jsonNode.DeepClone();
        }
        catch (Exception ex)
        {
            _logger.FailedToFetchRemoteSchema(ex, TextSanitizer.SanitizeUrlForLogging(resolvedUrl));
            throw;
        }
    }

    /// <summary>
    /// Applies authentication credentials to an HTTP request.
    /// </summary>
    private void ApplyAuthentication(HttpRequestMessage request, IAuthenticationConfig auth)
    {
        // Validate authentication data early to prevent propagation of tainted values
        if (auth == null)
            return;

        // Apply API Key authentication
        if (!string.IsNullOrEmpty(auth.ApiKey) && !string.IsNullOrEmpty(auth.ApiKeyHeader))
        {
            // Validate header name before using it to prevent header injection
            if (!IsValidHeaderName(auth.ApiKeyHeader))
            {
                _logger.InvalidApiKeyHeaderName();
                return;
            }
            request.Headers.Add(auth.ApiKeyHeader, auth.ApiKey);
            _logger.AppliedApiKeyAuthentication();
        }

        // Apply Bearer Token authentication
        if (!string.IsNullOrEmpty(auth.BearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.BearerToken);
            RemoteSchemaLoaderLog.AppliedBearerTokenAuthentication(_logger);
        }

        // Apply Basic authentication
        if (auth.BasicAuth != null && !string.IsNullOrEmpty(auth.BasicAuth.Username) && !string.IsNullOrEmpty(auth.BasicAuth.Password))
        {
            var credentials = Convert.ToBase64String(
                Encoding.ASCII.GetBytes($"{auth.BasicAuth.Username}:{auth.BasicAuth.Password}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            RemoteSchemaLoaderLog.AppliedBasicAuthentication(_logger);
        }

        // Apply custom headers
        if (auth.CustomHeaders != null)
        {
            foreach (var header in auth.CustomHeaders)
            {
                // Validate header name
                if (!IsValidHeaderName(header.Key))
                {
                    _logger.InvalidCustomHeaderName(header.Key);
                    continue;
                }
                request.Headers.Add(header.Key, header.Value);
                RemoteSchemaLoaderLog.AppliedCustomHeader(_logger, header.Key);
            }
        }
    }

    /// <summary>
    /// Validates that authentication configuration is present and properly formed.
    /// </summary>
    private static bool IsValidAuthentication(IAuthenticationConfig? auth)
    {
        if (auth == null)
        {
            return false;
        }

        // Validate that at least one authentication method is configured
        var hasApiKey = !string.IsNullOrEmpty(auth.ApiKey) && !string.IsNullOrEmpty(auth.ApiKeyHeader);
        var hasBearerToken = !string.IsNullOrEmpty(auth.BearerToken);
        var hasBasicAuth = auth.BasicAuth != null && !string.IsNullOrEmpty(auth.BasicAuth.Username);
        var hasCustomHeaders = auth.CustomHeaders != null && auth.CustomHeaders.Count > 0;

        return hasApiKey || hasBearerToken || hasBasicAuth || hasCustomHeaders;
    }

    /// <summary>
    /// Validates HTTP header names to prevent header injection attacks.
    /// </summary>
    private static bool IsValidHeaderName(string headerName)
    {
        if (string.IsNullOrWhiteSpace(headerName))
        {
            return false;
        }

        // Header names must not contain control characters or colons
        // RFC 7230 section 3.2: header-field = field-name ":" OWS field-value OWS
        return !headerName.Any(c => char.IsControl(c) || c == ':' || c == '\r' || c == '\n');
    }

    /// <summary>
    /// Generates a cache key for a schema URL.
    /// </summary>
    private static string GenerateCacheKey(string schemaUrl)
    {
        return $"schema:{schemaUrl}";
    }

    private string? NormalizeKnownSchemaUrl(string schemaUrl)
    {
        var normalized = NormalizeAbsoluteUrl(schemaUrl);
        if (normalized == null)
        {
            return null;
        }

        if (_knownJsonSchemaUrls.Contains(normalized))
        {
            return normalized;
        }

        if (_warnOnUnknownJsonSchemaDraft &&
            IsJsonSchemaDraftUrl(normalized) &&
            _unknownDraftWarnings.Add(normalized))
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.UnknownJsonSchemaDraftUrl(TextSanitizer.SanitizeUrlForLogging(normalized));
            }
        }

        return null;
    }

    private static string? NormalizeAbsoluteUrl(string schemaUrl)
    {
        if (string.IsNullOrWhiteSpace(schemaUrl))
        {
            return null;
        }

        if (schemaUrl.Contains("json-everything.lib", StringComparison.OrdinalIgnoreCase))
        {
            schemaUrl = schemaUrl.Replace("json-everything.lib", "json-everything.net", StringComparison.OrdinalIgnoreCase);
        }

        if (!Uri.TryCreate(schemaUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        return uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
    }

    private static bool IsJsonSchemaDraftUrl(string absoluteUrl)
    {
        if (!Uri.TryCreate(absoluteUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return string.Equals(uri.Host, "json-schema.org", StringComparison.OrdinalIgnoreCase) &&
               uri.AbsolutePath.StartsWith("/draft/", StringComparison.OrdinalIgnoreCase);
    }

    private static class SchemaResolutionOptionsDefaults
    {
        public static readonly string[] KnownJsonSchemaUrls =
        [
            "https://json-schema.org/draft/2020-12/schema",
            "https://json-schema.org/draft/2020-12/meta/core",
            "https://json-schema.org/draft/2020-12/meta/applicator",
            "https://json-schema.org/draft/2020-12/meta/unevaluated",
            "https://json-schema.org/draft/2020-12/meta/validation",
            "https://json-schema.org/draft/2020-12/meta/meta-data",
            "https://json-schema.org/draft/2020-12/meta/format-annotation",
            "https://json-schema.org/draft/2020-12/meta/content"
        ];
    }
}
