using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Json.Schema;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenReferralApi.Core.Extensions;
using OpenReferralApi.Core.Helpers;
using OpenReferralApi.Core.Logging;
using YamlDotNet.Serialization;
namespace OpenReferralApi.Core.Services;

public interface IProfileDiscoveryService
{
    Task<ProfileDiscoveryResult> DiscoverFromBaseUrlAsync(
        string? ownSchemaUrl,
        string? baseUrl,
        DataSourceAuthentication? authentication = null,
        CancellationToken cancellationToken = default);

    ProfileDiscoveryResult GetExplicitProfile(string profileVersion);
}

public sealed class ProfileDiscoveryResult
{
    public string? HsdsProfileReason { get; init; }
    public string? HsdsProfileSchemaUrl { get; init; }
    public string? OpenApiSchemaContent { get; init; }
    public string? HsdsProfileSchemaContent { get; init; }
    public string? HsdsProfileVersion { get; init; }
    public bool UsedDefaultProfile { get; init; }
}

public partial class ProfileDiscoveryService(
    ILogger<ProfileDiscoveryService> logger,
    IHttpClientFactory httpClientFactory,
    IOptions<SpecificationOptions> specificationOptions,
    IOptions<OpenApiValidationServerOptions>? openApiValidationOptions = null,
    IMemoryCache? memoryCache = null,
    ISchemaResolverService? schemaResolverService = null) : IProfileDiscoveryService
{
    private readonly OpenApiValidationServerOptions _openApiValidationOptions = openApiValidationOptions?.Value ?? new OpenApiValidationServerOptions();

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));

    private readonly SpecificationOptions _specificationOptions = specificationOptions?.Value ?? throw new ArgumentNullException(nameof(specificationOptions));
    private static readonly string[] HSDS_VERSION_candidateTokens =
    [
        "x-hsds-version",
        "version",
        "info.x-hsds-version",
        "info.x-profile-version",
        "info.version"
    ];

    private static readonly string[] OpenApiCandidateTokens =
    [
        "x-hsds-version",
        "version",
        "info.x-hsds-version",
        "info.x-profile-version"
    ];

    public async Task<ProfileDiscoveryResult> DiscoverFromBaseUrlAsync(
        string? ownSchemaUrl,
        string? baseUrl,
        DataSourceAuthentication? authentication = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new ArgumentException("Base URL must be provided", nameof(baseUrl));
        }

        var normalizedBaseUrl = baseUrl?.TrimEnd('/');
        var needsSchema = _openApiValidationOptions.OwnSchemaValidation != OwnSchemaValidationMode.None;

        string? discoveredVersion = null;
        string? discoveredSchema = null;
        string? discoveryReason = null;
        string? candidateOpenApiSpecContent = null;
        bool usedDefaultProfile = false;

        if (!string.IsNullOrWhiteSpace(normalizedBaseUrl))
        {
            using var client = _httpClientFactory.CreateClient("OpenApiValidationService");
            var probePaths = BuildDiscoveryProbePaths(ownSchemaUrl);

            foreach (var path in probePaths)
            {
                var discoveryUrl = BuildAbsoluteUrl(normalizedBaseUrl, path);
                try
                {
                    if (logger.IsEnabled(LogLevel.Debug))
                    {
                        logger.ProbingStandardPath(TextSanitizer.SanitizeUrlForLogging(discoveryUrl));
                    }
                    using var request = new HttpRequestMessage(HttpMethod.Get, discoveryUrl);
                    ApplyAuthentication(request, authentication);
                    using var response = await client.SendAsync(request, cancellationToken);

                    if (!response.IsSuccessStatusCode) continue;

                    var content = await response.Content.ReadAsStringAsync(cancellationToken);

                    if (candidateOpenApiSpecContent == null && LooksLikeOpenApiSpec(content))
                    {
                        candidateOpenApiSpecContent = content;
                    }

                    // 2. Extract version only if we don't have one yet
                    if (discoveredVersion == null)
                    {
                        var extractedVersion = TryExtractPotentialHsdsProfileVersion(content);
                        if (extractedVersion != null)
                        {
                            discoveredVersion = MapProfileVersion(extractedVersion);
                            var reasonPath = string.IsNullOrEmpty(path) ? "root" : path;
                            discoveryReason = extractedVersion == discoveredVersion
                                ? $"HSDS version {discoveredVersion} discovered from base URL at {reasonPath}"
                                : $"HSDS version {discoveredVersion} (mapped from {extractedVersion}) discovered from base URL at {reasonPath}";
                        }
                    }

                    // 3. Extract schema if required and not yet found
                    if ((needsSchema || discoveredVersion == null) && discoveredSchema == null)
                    {
                        if (LooksLikeOpenApiSpec(content))
                        {
                            discoveredSchema = content;
                        }
                        else
                        {
                            // Attempt scraping for indirect schema (UI/Config)
                            discoveredSchema = await TryFetchIndirectSchemaAsync(client, content, normalizedBaseUrl, authentication, cancellationToken);

                            if (discoveredVersion == null && discoveredSchema != null)
                            {
                                var extractedVersion = TryExtractPotentialHsdsProfileVersion(discoveredSchema);
                                if (extractedVersion != null)
                                {
                                    discoveredVersion = MapProfileVersion(extractedVersion);
                                    var reasonPath = string.IsNullOrEmpty(path) ? "root" : path;
                                    discoveryReason = extractedVersion == discoveredVersion
                                        ? $"HSDS version {discoveredVersion} discovered from base URL at {reasonPath}"
                                        : $"HSDS version {discoveredVersion} (mapped from {extractedVersion}) discovered from base URL at {reasonPath}";
                                }
                            }
                        }
                    }

                    // 4. Check if we already have everything we need to stop
                    if (discoveredVersion != null && (!needsSchema || discoveredSchema != null))
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    if (logger.IsEnabled(LogLevel.Debug))
                    {
                        logger.ProbeFailed(ex, TextSanitizer.SanitizeUrlForLogging(normalizedBaseUrl), path);
                    }
                }
            }
        }

        if (string.IsNullOrWhiteSpace(discoveredVersion))
        {
            var extractedVersion = TryExtractProfileVersionFromSchemaUrl(ownSchemaUrl);
            if (!string.IsNullOrWhiteSpace(extractedVersion))
            {
                discoveredVersion = MapProfileVersion(extractedVersion);
                discoveryReason = extractedVersion == discoveredVersion
                    ? $"HSDS version {discoveredVersion} extracted from schema URL"
                    : $"HSDS version {discoveredVersion} (mapped from {extractedVersion}) extracted from schema URL";
            }
        }

        // Last resort before default profile fallback: infer profile version from OpenAPI spec content.
        if (string.IsNullOrWhiteSpace(discoveredVersion))
        {
            var fallbackSpecContent = discoveredSchema ?? candidateOpenApiSpecContent;
            var (versionFromOpenApiSpec, fromOpenapiField) = TryExtractProfileVersionFromOpenApiSpec(fallbackSpecContent);
            if (!string.IsNullOrWhiteSpace(versionFromOpenApiSpec))
            {
                discoveredVersion = MapProfileVersion(versionFromOpenApiSpec);

                if (fromOpenapiField)
                {
                    discoveryReason = versionFromOpenApiSpec == discoveredVersion
                        ? $"Warning: The HSDS schema version was incorrectly defined in the 'openapi' field. " +
                          $"Detected HSDS version {versionFromOpenApiSpec} from this field as a fallback. " +
                          "Please use an 'x-hsds-version' field in your OpenAPI spec to declare the HSDS version."
                        : $"Warning: The HSDS schema version was incorrectly defined in the 'openapi' field. " +
                          $"Detected HSDS version {discoveredVersion} (mapped from {versionFromOpenApiSpec}) from this field as a fallback. " +
                          "Please use an 'x-hsds-version' field in your OpenAPI spec to declare the HSDS version.";
                }
                else
                {
                    discoveryReason = versionFromOpenApiSpec == discoveredVersion
                        ? $"Standard version [user: {discoveredVersion}] read from OpenAPI spec"
                        : $"Standard version [user: {discoveredVersion}] (mapped from {versionFromOpenApiSpec}) read from OpenAPI spec";
                }
            }
        }

        // if we get to here with no profile version, use the default if one is configured.
        if (string.IsNullOrWhiteSpace(discoveredVersion))
        {
            var hasConfiguredDefaultProfile = TryGetDefaultProfileSchemaFallback(
                out _,
                out var defaultProfileVersion);

            if (hasConfiguredDefaultProfile)
            {
                usedDefaultProfile = true;
                discoveredVersion = defaultProfileVersion;
                discoveryReason = $"Using configured default HSDS profile version: {defaultProfileVersion}";
            }
            else
            {
                throw ProfileValidationErrors.NoProfileDiscoveredAndNoDefault();
            }
        }

        if (!TryGetSchemaUrlForProfileVersion(discoveredVersion!, out var hsdsProfileSchemaUrl))
        {
            var hasConfiguredDefaultProfile = TryGetDefaultProfileSchemaFallback(
                out _,
                out var defaultProfileVersion);

            if (hasConfiguredDefaultProfile && discoveredVersion != defaultProfileVersion)
            {
                usedDefaultProfile = true;
                var oldDiscoveredVersion = discoveredVersion;
                discoveredVersion = defaultProfileVersion;
                discoveryReason = $"Discovered profile '{oldDiscoveredVersion}' is not supported. Falling back to configured default HSDS profile version: {defaultProfileVersion}";

                if (!TryGetSchemaUrlForProfileVersion(discoveredVersion!, out hsdsProfileSchemaUrl))
                {
                    throw ProfileValidationErrors.DiscoveredAndDefaultUnsupported(oldDiscoveredVersion, defaultProfileVersion);
                }
            }
            else
            {
                throw ProfileValidationErrors.DiscoveredUnsupported(discoveredVersion);
            }
        }

         var hsdsProfileSchemaContent = await GetHsdsProfileSchemaContentAsync(discoveredVersion, cancellationToken);
        if (string.IsNullOrWhiteSpace(hsdsProfileSchemaContent))
        {
            throw ProfileValidationErrors.ProfileSchemaNotCached(discoveredVersion);
        }

        discoveryReason ??= "Discovery completed with available information.";
        discoveredVersion ??= "unknown version";  // should never be null/empty here due to fallback logic, but just in case

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.ProfileDiscoveryResolved(TextSanitizer.SanitizeUrlForLogging(hsdsProfileSchemaUrl), discoveredVersion, discoveryReason);
        }

        return new ProfileDiscoveryResult
        {
            HsdsProfileVersion = discoveredVersion,
            HsdsProfileSchemaUrl = hsdsProfileSchemaUrl,
            OpenApiSchemaContent = discoveredSchema,
            HsdsProfileSchemaContent = hsdsProfileSchemaContent,
            HsdsProfileReason = discoveryReason,
            UsedDefaultProfile = usedDefaultProfile
        };
    }

    public ProfileDiscoveryResult GetExplicitProfile(string profileVersion)
    {
        if (string.IsNullOrWhiteSpace(profileVersion))
        {
            throw new ArgumentException("Profile version cannot be empty", nameof(profileVersion));
        }

        if (!TryGetSchemaUrlForProfileVersion(profileVersion, out var hsdsProfileSchemaUrl))
        {
            throw ProfileValidationErrors.DiscoveredUnsupported(profileVersion);
        }

        string? hsdsProfileSchemaContent = null;
        if (memoryCache != null)
        {
            foreach (var cacheKey in GetSchemaCacheKeyCandidates(hsdsProfileSchemaUrl))
            {
                if (memoryCache.TryGetValue<CachedSchema>(cacheKey, out var cachedSchema)
                    && cachedSchema != null
                    && !string.IsNullOrWhiteSpace(cachedSchema.RawJson))
                {
                    hsdsProfileSchemaContent = cachedSchema.RawJson;
                    break;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(hsdsProfileSchemaContent))
        {
            throw ProfileValidationErrors.ProfileSchemaNotCached(profileVersion);
        }

        return new ProfileDiscoveryResult
        {
            HsdsProfileVersion = profileVersion,
            HsdsProfileSchemaUrl = hsdsProfileSchemaUrl,
            OpenApiSchemaContent = null,
            HsdsProfileSchemaContent = hsdsProfileSchemaContent,
            HsdsProfileReason = $"Explicit profile '{profileVersion}' provided in request.",
            UsedDefaultProfile = false
        };
    }

    private async Task<string?> GetHsdsProfileSchemaContentAsync(string? hsdsProfileVersion, CancellationToken cancellationToken)
    {
        if (memoryCache == null)
        {
            return null;
        }

        var profileVersion = hsdsProfileVersion?.Trim();
        if (string.IsNullOrWhiteSpace(profileVersion))
        {
            return null;
        }

        if (!TryGetSchemaUrlForProfileVersion(profileVersion, out var schemaUrl)
            || string.IsNullOrWhiteSpace(schemaUrl))
        {
            return null;
        }

        foreach (var cacheKey in GetSchemaCacheKeyCandidates(schemaUrl))
        {
            if (memoryCache.TryGetValue<CachedSchema>(cacheKey, out var cachedSchema)
                && cachedSchema != null
                && !string.IsNullOrWhiteSpace(cachedSchema.RawJson))
            {
                return cachedSchema.RawJson;
            }
        }

        try
        {
            logger.ProfileSchemaCacheMiss(profileVersion, schemaUrl);
            using var client = _httpClientFactory.CreateClient("OpenApiValidationService");
            using var response = await client.GetAsync(schemaUrl, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var jsonNode = JsonNode.Parse(content);
                if (jsonNode != null)
                {
                    var cacheEntryOptions = new MemoryCacheEntryOptions
                    {
                        Size = content.Length,
                        Priority = CacheItemPriority.Normal
                    };

                    // Put placeholder in cache to prevent circular recursion/deadlocks during compilation
                    var placeholder = new CachedSchema(new JsonSchemaBuilder().Build(), jsonNode, content, content.Length);
                    memoryCache.Set($"schema:{schemaUrl}", placeholder, cacheEntryOptions);

                    var schema = schemaResolverService != null
                        ? await schemaResolverService.CreateSchemaFromJsonAsync(content, schemaUrl, auth: null, cancellationToken)
                        : JsonSchemaBuild.FromText(content);
                    var cachedSchema = new CachedSchema(schema, jsonNode, content, content.Length);

                    memoryCache.Set($"schema:{schemaUrl}", cachedSchema, cacheEntryOptions);

                    try
                    {
                        if (Json.Schema.SchemaRegistry.Global.Get(new Uri(schemaUrl)) == null)
                        {
                            Json.Schema.SchemaRegistry.Global.Register(new Uri(schemaUrl), schema);
                        }
                    }
                    catch { /* Ignore */ }

                    return content;
                }
            }
            else
            {
                logger.FailedToFetchProfileSchema(schemaUrl, (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            logger.ErrorFetchingProfileSchema(ex, schemaUrl);
        }

        return null;
    }

    private bool TryGetSchemaUrlForProfileVersion(string hsdsProfileVersion, out string schemaUrl)
    {
        schemaUrl = string.Empty;

        if (_specificationOptions.Urls.TryGetValue(hsdsProfileVersion, out var directUrl)
            && !string.IsNullOrWhiteSpace(directUrl))
        {
            schemaUrl = directUrl;
            return true;
        }

        foreach (var mapping in _specificationOptions.Urls)
        {
            if (string.Equals(mapping.Key, hsdsProfileVersion, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(mapping.Value))
            {
                schemaUrl = mapping.Value;
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> GetSchemaCacheKeyCandidates(string schemaUrl)
    {
        yield return $"schema:{schemaUrl}";

        if (Uri.TryCreate(schemaUrl, UriKind.Absolute, out var schemaUri))
        {
            var normalizedUrl = schemaUri.GetLeftPart(UriPartial.Path).TrimEnd('/');
            if (!string.Equals(normalizedUrl, schemaUrl, StringComparison.Ordinal))
            {
                yield return $"schema:{normalizedUrl}";
            }
        }
    }

    private static IReadOnlyList<string> BuildDiscoveryProbePaths(string? ownSchemaUrl = null)
    {
        var paths = ExpandSpecPaths(Constants.OpenApiDocumentProbePaths)
            .Concat(Constants.SwaggerConfigProbePaths)
            .Concat(Constants.DocumentationUiProbePaths)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(ownSchemaUrl))
        {
            paths = new[] { ownSchemaUrl }.Concat(paths).Distinct(StringComparer.OrdinalIgnoreCase);
        }

        return [.. paths];
    }

    private static IEnumerable<string> ExpandSpecPaths(IEnumerable<string> basePaths)
    {
        foreach (var path in basePaths)
        {
            yield return path;

            if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                var withoutJsonExt = path[..^".json".Length];
                yield return withoutJsonExt + ".yaml";
                yield return withoutJsonExt + ".yml";
            }
        }
    }

    private static bool LooksLikeOpenApiSpec(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        var trimmed = content.TrimStart();
        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
        {
            return content.Contains("\"openapi\"", StringComparison.OrdinalIgnoreCase)
                   || content.Contains("\"swagger\"", StringComparison.OrdinalIgnoreCase);
        }

        return Constants.OpenApiYamlRegex.IsMatch(content);
    }

    private static string? TryExtractPotentialHsdsProfileVersion(string specContent)
    {
        var jsonContent = EnsureJson(specContent);
        if (string.IsNullOrWhiteSpace(jsonContent))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(jsonContent);
            var root = document.RootElement;

            foreach (var path in HSDS_VERSION_candidateTokens)
            {
                var value = root.TryGetPathString(path);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static (string? version, bool fromOpenapiField) TryExtractProfileVersionFromOpenApiSpec(string? openApiSpecContent)
    {
        var jsonContent = EnsureJson(openApiSpecContent ?? string.Empty);
        if (string.IsNullOrWhiteSpace(jsonContent))
        {
            return (null, false);
        }

        try
        {
            using var document = JsonDocument.Parse(jsonContent);
            var root = document.RootElement;


            foreach (var tokenPath in OpenApiCandidateTokens)
            {
                var tokenValue = root.TryGetPathString(tokenPath);
                if (!string.IsNullOrWhiteSpace(tokenValue))
                {
                    return (tokenValue, false);
                }
            }

            var openapiValue = root.TryGetPathString("openapi");
            if (!string.IsNullOrWhiteSpace(openapiValue))
            {
                var parts = openapiValue.Split('.');
                if (parts.Length >= 2)
                {
                    var majorMinor = $"{parts[0]}.{parts[1]}";
                    if (!string.IsNullOrWhiteSpace(majorMinor))
                    {
                        return (majorMinor, true);
                    }
                }
            }
        }
        catch
        {
            return (null, false);
        }

        return (null, false);
    }

    private async Task<string?> TryFetchIndirectSchemaAsync(
        HttpClient client,
        string content,
        string baseUrl,
        DataSourceAuthentication? authentication,
        CancellationToken cancellationToken)
    {
        // 1. Check if the content is a Swagger Config JSON
        var configUrls = DiscoverFromSwaggerConfigContent(content, baseUrl);
        if (configUrls.Count > 0)
        {
            // Try the first URL found in the config
            var spec = await TryFetchDiscoveredSpecContentAsync(client, configUrls[0], authentication, cancellationToken);
            if (spec != null) return spec;
        }

        // 2. Check if the content is HTML (Swagger UI / Redoc)
        var htmlSpecUrl = await DiscoverFromUiHtmlAsync(client, content, baseUrl, authentication, cancellationToken);
        if (!string.IsNullOrWhiteSpace(htmlSpecUrl))
        {
            return await TryFetchDiscoveredSpecContentAsync(client, htmlSpecUrl, authentication, cancellationToken);
        }

        return null;
    }

    private async Task<string?> TryFetchDiscoveredSpecContentAsync(
        HttpClient client,
        string specUrl,
        DataSourceAuthentication? authentication,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, specUrl);
            ApplyAuthentication(request, authentication);
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.DiscoveredSpecUrlReturnedStatusCode(TextSanitizer.SanitizeUrlForLogging(specUrl), (int)response.StatusCode);
                }
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (LooksLikeOpenApiSpec(content))
            {
                return content;
            }

            // If the response is not JSON or an OpenAPI spec, parse the HTML and test if it is another specification such as Swagger UI
            var extractedSpec = await TryFetchIndirectSchemaAsync(client, content, specUrl, authentication, cancellationToken);
            return extractedSpec;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.FailedToFetchDiscoveredSpecContent(ex, TextSanitizer.SanitizeUrlForLogging(specUrl));
            }
            return null;
        }
    }

    private async Task<string?> DiscoverFromUiHtmlAsync(
        HttpClient client,
        string htmlContent,
        string baseUrl,
        DataSourceAuthentication? authentication,
        CancellationToken cancellationToken)
    {
        var discoveredUrls = await DiscoverAllDefinitionsAsync(htmlContent, baseUrl);
        if (discoveredUrls.Count > 0)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                foreach (var discoveredUrl in discoveredUrls)
                {
                    logger.DiscoveredCandidateFromUiHtml(TextSanitizer.SanitizeUrlForLogging(discoveredUrl));
                }
            }

            return discoveredUrls[0];
        }

        var configUrls = DiscoverConfigUrls(htmlContent, baseUrl);
        foreach (var configUrl in configUrls)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.DiscoveredSwaggerConfigCandidate(TextSanitizer.SanitizeUrlForLogging(configUrl));
            }
            var discoveredFromConfig = await DiscoverFromSwaggerConfigEndpointAsync(client, baseUrl, configUrl, authentication, cancellationToken);
            if (discoveredFromConfig.Count > 0)
            {
                return discoveredFromConfig[0];
            }
        }

        return null;
    }

    private async Task<List<string>> DiscoverFromSwaggerConfigEndpointAsync(
        HttpClient client,
        string baseUrl,
        string configUrl,
        DataSourceAuthentication? authentication,
        CancellationToken cancellationToken)
    {
        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.RequestingSwaggerConfigEndpoint(TextSanitizer.SanitizeUrlForLogging(configUrl));
        }
        using var request = new HttpRequestMessage(HttpMethod.Get, configUrl);
        ApplyAuthentication(request, authentication);
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.SwaggerConfigEndpointReturnedStatusCode(TextSanitizer.SanitizeUrlForLogging(configUrl), (int)response.StatusCode);
            }
            return [];
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var discovered = DiscoverFromSwaggerConfigContent(content, baseUrl);
        if (logger.IsEnabled(LogLevel.Debug))
        {
            foreach (var discoveredUrl in discovered)
            {
                logger.DiscoveredCandidateFromSwaggerConfig(TextSanitizer.SanitizeUrlForLogging(configUrl), TextSanitizer.SanitizeUrlForLogging(discoveredUrl));
            }
        }

        return discovered;
    }

    private static List<string> DiscoverFromSwaggerConfigContent(string configContent, string baseUrl)
    {
        var discoveredUrls = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var jsonContent = EnsureJson(configContent);
        if (string.IsNullOrWhiteSpace(jsonContent))
        {
            return discoveredUrls;
        }

        try
        {
            using var document = JsonDocument.Parse(jsonContent);
            var root = document.RootElement;

            AddResolvedUrl(TryGetString(root, "url"), baseUrl, seen, discoveredUrls);

            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("urls", out var urls)
                && urls.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in urls.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    AddResolvedUrl(TryGetString(item, "url"), baseUrl, seen, discoveredUrls);
                }
            }
        }
        catch
        {
            return discoveredUrls;
        }

        return discoveredUrls;
    }

    private static List<string> DiscoverConfigUrls(string htmlContent, string baseUrl)
    {
        var discoveredUrls = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in Constants.SwaggerUiConfigUrlRegex.Matches(htmlContent))
        {
            AddResolvedUrl(match.Groups[1].Value, baseUrl, seen, discoveredUrls);
        }

        return discoveredUrls;
    }

    private static Task<List<string>> DiscoverAllDefinitionsAsync(string uiHtml, string baseUri)
    {
        var discoveredUrls = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(uiHtml))
        {
            return Task.FromResult(discoveredUrls);
        }

        foreach (Match match in Constants.SwaggerUiDefinitionUrlRegex.Matches(uiHtml))
        {
            var path = match.Groups[1].Value;
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            var absoluteUrl = new Uri(new Uri(baseUri + "/"), path).ToString();
            if (seen.Add(absoluteUrl))
            {
                discoveredUrls.Add(absoluteUrl);
            }
        }

        foreach (Match match in Constants.SwaggerUiApiDocsRegex.Matches(uiHtml))
        {
            AddResolvedUrl(match.Groups[1].Value, baseUri, seen, discoveredUrls);
        }

        foreach (Match match in Constants.SpecUrlAttributeRegex.Matches(uiHtml))
        {
            AddResolvedUrl(match.Groups[1].Value, baseUri, seen, discoveredUrls);
        }

        foreach (Match match in Constants.RedocInitRegex.Matches(uiHtml))
        {
            AddResolvedUrl(match.Groups[1].Value, baseUri, seen, discoveredUrls);
        }

        return Task.FromResult(discoveredUrls);
    }

    private static void AddResolvedUrl(string? path, string baseUri, HashSet<string> seen, List<string> output)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var absoluteUrl = new Uri(new Uri(baseUri + "/"), path).ToString();
        if (seen.Add(absoluteUrl))
        {
            output.Add(absoluteUrl);
        }
    }

    private static string BuildAbsoluteUrl(string baseUrl, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return baseUrl;

        if (Uri.IsWellFormedUriString(relativePath, UriKind.Absolute))
            return relativePath;

        return $"{baseUrl}/{relativePath.TrimStart('/')}";
    }

    private static void ApplyAuthentication(HttpRequestMessage request, DataSourceAuthentication? auth)
    {
        if (auth == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(auth.ApiKey)
            && !string.IsNullOrWhiteSpace(auth.ApiKeyHeader)
            && IsValidHeaderName(auth.ApiKeyHeader))
        {
            _ = request.Headers.TryAddWithoutValidation(auth.ApiKeyHeader, auth.ApiKey);
        }

        if (!string.IsNullOrWhiteSpace(auth.BearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.BearerToken);
        }

        if (auth.BasicAuth != null
            && !string.IsNullOrWhiteSpace(auth.BasicAuth.Username)
            && !string.IsNullOrWhiteSpace(auth.BasicAuth.Password))
        {
            var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{auth.BasicAuth.Username}:{auth.BasicAuth.Password}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        }

        if (auth.CustomHeaders == null)
        {
            return;
        }

        foreach (var header in auth.CustomHeaders)
        {
            if (string.IsNullOrWhiteSpace(header.Value) || !IsValidHeaderName(header.Key))
            {
                continue;
            }

            _ = request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
    }

    private static bool IsValidHeaderName(string headerName)
    {
        if (string.IsNullOrWhiteSpace(headerName))
        {
            return false;
        }

        return !headerName.Any(c => char.IsControl(c) || c == ':' || c == '\r' || c == '\n');
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.GetRawText(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => null
        };
    }

    [GeneratedRegex(@"/specifications/(?<version>[^/]+)/openapi\.json", RegexOptions.IgnoreCase)]
    private static partial Regex SchemaUrlVersionRegex();

    private static string? TryExtractProfileVersionFromSchemaUrl(string? schemaUrl)
    {
        if (string.IsNullOrWhiteSpace(schemaUrl))
        {
            return null;
        }

        var match = SchemaUrlVersionRegex().Match(schemaUrl);

        if (!match.Success)
        {
            return null;
        }

        var extracted = match.Groups["version"].Value.Trim();
        return string.IsNullOrWhiteSpace(extracted) ? null : extracted;
    }

    private string MapProfileVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return version;
        }

        if (_specificationOptions.ProfileVersionMappings == null || _specificationOptions.ProfileVersionMappings.Count == 0)
        {
            return version;
        }

        foreach (var mapping in _specificationOptions.ProfileVersionMappings)
        {
            var targetVersion = mapping.Key;
            var aliases = mapping.Value;
            if (aliases == null) continue;

            foreach (var alias in aliases)
            {
                if (string.Equals(alias, version, StringComparison.OrdinalIgnoreCase))
                {
                    return targetVersion;
                }
            }
        }

        return version;
    }

    private bool TryGetDefaultProfileSchemaFallback(out string schemaUrl, out string? profileVersion)
    {
        schemaUrl = string.Empty;
        profileVersion = null;

        if (string.IsNullOrWhiteSpace(_specificationOptions.DefaultProfileVersion) || _specificationOptions.Urls.Count == 0)
        {
            return false;
        }

        var configuredDefaultKey = _specificationOptions.DefaultProfileVersion.Trim();
        if (!_specificationOptions.Urls.TryGetValue(configuredDefaultKey, out var configuredDefaultSchemaUrl)
            || string.IsNullOrWhiteSpace(configuredDefaultSchemaUrl)
            || !Uri.IsWellFormedUriString(configuredDefaultSchemaUrl, UriKind.Absolute))
        {
            logger.InvalidDefaultProfileVersion(TextSanitizer.SanitizeStringForLogging(configuredDefaultKey));
            return false;
        }

        schemaUrl = configuredDefaultSchemaUrl;
        profileVersion = configuredDefaultKey;

        return true;
    }

    private static string? EnsureJson(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var trimmed = content.TrimStart();
        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
        {
            return content;
        }

        // Fast path to reject obvious HTML/XML without throwing exceptions
        if (trimmed.StartsWith('<'))
        {
            return null;
        }

        try
        {
            var deserializer = new DeserializerBuilder()
                .Build();
            var yamlObject = deserializer.Deserialize(new System.IO.StringReader(content));
            if (yamlObject == null) return null;

            var serializer = new SerializerBuilder().JsonCompatible().Build();
            return serializer.Serialize(yamlObject);
        }
        catch (YamlDotNet.Core.YamlException)
        {
            // We only reach here if it wasn't JSON and wasn't HTML, meaning it was likely 
            // intended to be YAML but had a syntax error.
            return null;
        }
        catch
        {
            return null;
        }
    }
}
