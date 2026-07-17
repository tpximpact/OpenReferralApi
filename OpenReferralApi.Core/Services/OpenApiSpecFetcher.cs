using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using OpenReferralApi.Core.Helpers;
using OpenReferralApi.Core.Logging;
using YamlDotNet.Serialization;

namespace OpenReferralApi.Core.Services;

/// <summary>
/// Internal helper class for fetching and parsing OpenAPI specifications from remote URLs.
/// Handles authentication and reference resolution.
/// </summary>
public class OpenApiSpecFetcher(
    IHttpClientFactory httpClientFactory,
    ILogger logger,
    ISchemaResolverService schemaResolverService,
    bool allowUserSuppliedAuth)
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly ISchemaResolverService _schemaResolverService = schemaResolverService ?? throw new ArgumentNullException(nameof(schemaResolverService));
    private readonly bool _allowUserSuppliedAuth = allowUserSuppliedAuth;

    /// <summary>
    /// Validates user-supplied authentication according to server-side policy.
    /// Returns a validated authentication object or null if no authentication should be applied.
    /// </summary>
    /// <param name="safeSpecUrl">Sanitized URL used only for logging.</param>
    /// <param name="auth">User-supplied authentication details.</param>
    /// <param name="isHttps">Whether the target URL is HTTPS.</param>
    private DataSourceAuthentication? TryGetValidatedAuthentication(
        string safeSpecUrl,
        DataSourceAuthentication? auth,
        bool isHttps)
    {
        if (auth == null)
        {
            return null;
        }

        // If server-side configuration does not allow user-supplied authentication,
        // treat this as non-fatal and simply skip applying any credentials.
        if (!_allowUserSuppliedAuth)
        {
            _logger.AuthDisabledByServerConfig(safeSpecUrl);
            return null;
        }

        // Enforce HTTPS requirement when sending authentication credentials.
        if (!isHttps)
        {
            _logger.RefusingNonHttpsAuth(safeSpecUrl);
            throw new InvalidOperationException(
                $"Authentication credentials may only be used with HTTPS endpoints. URL: {safeSpecUrl}");
        }

        // Only validated authentication information is used to guard sensitive operations.
        var validatedAuth = ValidateAuthentication(auth);
        return validatedAuth;
    }

    /// <summary>
    /// Fetches and optionally resolves an OpenAPI specification from a URL.
    /// </summary>
    /// <param name="specUrl">The URL of the OpenAPI specification</param>
    /// <param name="auth">Optional authentication credentials</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <param name="resolveReferences">Whether to resolve $ref references (true by default)</param>
    /// <returns>The parsed OpenAPI specification as JsonObject</returns>
    public async Task<JsonObject> FetchOpenApiSpecFromUrlAsync(
        string specUrl,
        DataSourceAuthentication? auth,
        CancellationToken cancellationToken,
        bool resolveReferences = true)
    {
        try
        {
            var safeSpecUrl = TextSanitizer.SanitizeUrlForLogging(specUrl);
            _logger.FetchingOpenApiSpec(safeSpecUrl);

            if (!Uri.IsWellFormedUriString(specUrl, UriKind.Absolute))
            {
                throw new ArgumentException($"Invalid OpenAPI spec URL: {safeSpecUrl}");
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, specUrl);

            // Apply authentication only if it is allowed by server configuration, the target
            // URL uses HTTPS, and the authentication details pass strict validation.
            DataSourceAuthentication? validatedAuth = null;

            // Determine whether the target URL is using HTTPS. This is used as an additional
            // server-side policy check before allowing any user-supplied authentication details
            // to be sent to an external endpoint.
            var uri = new Uri(specUrl);
            var isHttps = string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

            // Server-side policy: only consider applying authentication if the feature is enabled.
            var shouldApplyAuth = _allowUserSuppliedAuth && isHttps;

            // Never decide to apply authentication based solely on whether user-supplied
            // credentials are present. Instead, require that server-side policy allows
            // user-supplied authentication and that the credentials pass strict validation.
            if (!_allowUserSuppliedAuth && auth != null)
            {
                _logger.AuthDisabledForRequest(safeSpecUrl);
            }
            else if (shouldApplyAuth)
            {
                validatedAuth = TryGetValidatedAuthentication(safeSpecUrl, auth, isHttps);
                if (validatedAuth != null)
                {
                    ApplyAuthentication(request, validatedAuth);
                }
            }

            var httpClient = _httpClientFactory.CreateClient(nameof(OpenApiValidationService));
            using var response = await httpClient.SendAsync(request, cancellationToken);
            _ = response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var normalizedContent = EnsureJson(content);

            // Only resolve references if requested (lazy evaluation)
            // This avoids expensive resolution when we're only validating spec structure
            // or when endpoints won't be tested
            if (resolveReferences)
            {
                var resolvedContent = await _schemaResolverService.ResolveAsync(normalizedContent, specUrl, validatedAuth);
                return ParseJsonObject(EnsureJson(resolvedContent));
            }

            // Return unresolved document for spec validation or later lazy resolution
            return ParseJsonObject(normalizedContent);
        }
        catch (Exception ex)
        {
            var sanitizedSpecUrl = TextSanitizer.SanitizeUrlForLogging(specUrl);
            _logger.FailedToFetchOpenApiSpec(ex, sanitizedSpecUrl);
            throw new InvalidOperationException($"Failed to fetch OpenAPI specification from URL: {sanitizedSpecUrl}", ex);
        }
    }

    /// <summary>
    /// Validates authentication configuration before use.
    /// Only returns a non-null value if the configuration passes strict validation.
    /// </summary>
    private static DataSourceAuthentication? ValidateAuthentication(DataSourceAuthentication? auth)
    {
        if (auth == null)
        {
            return null;
        }

        // Normalize simple string fields
        if (!string.IsNullOrWhiteSpace(auth.ApiKey))
        {
            auth.ApiKey = auth.ApiKey.Trim();
        }

        if (!string.IsNullOrWhiteSpace(auth.BearerToken))
        {
            auth.BearerToken = auth.BearerToken.Trim();
        }

        if (auth.BasicAuth != null)
        {
            if (!string.IsNullOrWhiteSpace(auth.BasicAuth.Username))
            {
                auth.BasicAuth.Username = auth.BasicAuth.Username.Trim();
            }

            if (!string.IsNullOrWhiteSpace(auth.BasicAuth.Password))
            {
                auth.BasicAuth.Password = auth.BasicAuth.Password.Trim();
            }
        }

        // Simple length limits to avoid abuse
        static bool IsTooLong(string? value, int maxLength) =>
            !string.IsNullOrEmpty(value) && value.Length > maxLength;

        const int MaxTokenLength = 4096;
        if (IsTooLong(auth.ApiKey, MaxTokenLength) ||
            IsTooLong(auth.BearerToken, MaxTokenLength) ||
            (auth.BasicAuth != null &&
                (IsTooLong(auth.BasicAuth.Username, MaxTokenLength) ||
                 IsTooLong(auth.BasicAuth.Password, MaxTokenLength))))
        {
            // Reject unreasonably large auth values
            return null;
        }

        // Validate custom headers against a conservative allowlist
        var hasCustomHeaders = false;
        if (auth.CustomHeaders != null && auth.CustomHeaders.Count > 0)
        {
            var allowedHeaderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Authorization",
                "X-API-Key",
                "X-Api-Key"
            };

            var sanitizedHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in auth.CustomHeaders)
            {
                var name = kvp.Key?.Trim();
                var value = kvp.Value;

                if (string.IsNullOrWhiteSpace(name) || !allowedHeaderNames.Contains(name))
                {
                    // Reject any disallowed or malformed header
                    return null;
                }

                if (string.IsNullOrEmpty(value) || IsTooLong(value, MaxTokenLength))
                {
                    return null;
                }

                sanitizedHeaders[name] = value;
            }

            if (sanitizedHeaders.Count > 0)
            {
                auth.CustomHeaders = sanitizedHeaders;
                hasCustomHeaders = true;
            }
        }

        // Determine whether there is at least one valid authentication mechanism configured
        var hasApiKey = !string.IsNullOrEmpty(auth.ApiKey);
        var hasBearerToken = !string.IsNullOrEmpty(auth.BearerToken);
        var hasBasicAuth = auth.BasicAuth != null && !string.IsNullOrEmpty(auth.BasicAuth.Username);

        if (hasApiKey || hasBearerToken || hasBasicAuth || hasCustomHeaders)
        {
            return auth;
        }

        return null;
    }

    private static string EnsureJson(string rawContent)
    {
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            throw new FormatException("OpenAPI spec content was empty.");
        }

        var trimmedContent = rawContent.TrimStart();
        if (trimmedContent.StartsWith('{') ||
            trimmedContent.StartsWith('['))
        {
            return rawContent;
        }

        // Fast path to reject obvious HTML/XML without throwing Yaml exceptions
        if (trimmedContent.StartsWith('<'))
        {
            throw new FormatException("OpenAPI spec content appears to be HTML/XML.");
        }

        try
        {
            var deserializer = new DeserializerBuilder()
                .Build();
            var yamlObject = deserializer.Deserialize(new StringReader(rawContent));

            var serializer = new SerializerBuilder()
                .JsonCompatible()
                .Build();

            return serializer.Serialize(yamlObject);
        }
        catch (Exception ex)
        {
            throw new FormatException("OpenAPI spec content was neither valid JSON nor valid YAML.", ex);
        }
    }

    private static JsonObject ParseJsonObject(string json)
    {
        if (JsonNode.Parse(json) is not JsonObject obj)
        {
            throw new FormatException("OpenAPI spec content must be a JSON object.");
        }

        return obj;
    }

    /// <summary>
    /// Applies authentication credentials to an HTTP request.
    /// </summary>
    private void ApplyAuthentication(HttpRequestMessage request, DataSourceAuthentication auth)
    {
        // Apply API Key authentication
        if (!string.IsNullOrEmpty(auth.ApiKey))
        {
            request.Headers.Add(auth.ApiKeyHeader, auth.ApiKey);
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.AppliedApiKeyAuthentication(TextSanitizer.SanitizeStringForLogging(auth.ApiKeyHeader));
            }
        }

        // Apply Bearer Token authentication
        if (!string.IsNullOrEmpty(auth.BearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.BearerToken);
            OpenApiSpecFetcherLog.AppliedBearerTokenAuthentication(_logger);
        }

        // Apply Basic authentication
        if (auth.BasicAuth != null && !string.IsNullOrEmpty(auth.BasicAuth.Username))
        {
            var credentials = Convert.ToBase64String(
                Encoding.ASCII.GetBytes($"{auth.BasicAuth.Username}:{auth.BasicAuth.Password}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            // Intentionally avoid logging user-supplied authentication identifiers
            OpenApiSpecFetcherLog.AppliedBasicAuthentication(_logger);
        }

        // Apply custom headers
        if (auth.CustomHeaders != null)
        {
            foreach (var header in auth.CustomHeaders)
            {
                request.Headers.Add(header.Key, header.Value);
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    OpenApiSpecFetcherLog.AppliedCustomHeader(_logger, TextSanitizer.SanitizeStringForLogging(header.Key));
                }
            }
        }
    }
}
