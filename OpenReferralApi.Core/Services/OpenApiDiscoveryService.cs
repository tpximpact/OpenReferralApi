using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace OpenReferralApi.Core.Services;

public interface IOpenApiDiscoveryService
{
    /// <summary>
    /// Attempts to discover an OpenAPI schema URL from the provided base URL.
    /// Returns the discovered URL or null if none found.
    /// </summary>
    Task<string?> DiscoverOpenApiUrlAsync(string baseUrl, CancellationToken cancellationToken = default);
}

public class OpenApiDiscoveryService : IOpenApiDiscoveryService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OpenApiDiscoveryService> _logger;

    public OpenApiDiscoveryService(IHttpClientFactory httpClientFactory, ILogger<OpenApiDiscoveryService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<string?> DiscoverOpenApiUrlAsync(string baseUrl, CancellationToken cancellationToken = default)
    {
        const string baseSpecificationUrl = "https://raw.githubusercontent.com/tpximpact/OpenReferralApi/refs/heads/validate_using_openapi/OpenReferralApi/Schemas/";

        if (string.IsNullOrWhiteSpace(baseUrl)) return null;

        const float defaultSpecificationVersion = 1.0f;
        var defaultSpec = $"{baseSpecificationUrl}V{defaultSpecificationVersion:0.0}-UK/open_api.json";
        try
        {
            using var httpClient = _httpClientFactory?.CreateClient("OpenApiValidationService") ?? new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);
            _logger.LogInformation("Requesting BaseUrl to discover openapi_url: {BaseUrl}", baseUrl);
            var resp = await httpClient.GetAsync(baseUrl, cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogInformation("BaseUrl request returned {Status}; defaulting to HSDS-UK 1.0 spec: {DefaultSpec}", resp.StatusCode, defaultSpec);
                return defaultSpec;
            }

            var content = await resp.Content.ReadAsStringAsync(cancellationToken);
            try
            {
                var j = JObject.Parse(content);

                // Check for version field and construct URL
                var versionToken = j.SelectToken("version");
                var version = versionToken?.ToString();
                if (!string.IsNullOrEmpty(version))
                {
                    var extractedVersion = ExtractVersionNumber(version);
                    if (extractedVersion.HasValue)
                    {
                        var versionedSpec = $"{baseSpecificationUrl}{extractedVersion.Value:0.0}/openapi.json";
                        _logger.LogInformation("Detected version '{Version}'; using HSDS-UK {ExtractedVersion:0.0} spec: {OpenApiUrl}", version, extractedVersion.Value, versionedSpec);
                        return versionedSpec;
                    }
                }

                // Check for explicit openapi_url field first
                var openapiUrlToken = j.SelectToken("openapi_url") ?? j.SelectToken("openapiUrl") ?? j.SelectToken("open_api_url");
                var openapiUrl = openapiUrlToken?.ToString();
                if (!string.IsNullOrEmpty(openapiUrl))
                {
                    _logger.LogInformation("Discovered openapi_url: {OpenApiUrl}", openapiUrl);
                    return openapiUrl;
                }

                _logger.LogInformation("No openapi_url or version in BaseUrl response; defaulting to HSDS-UK 1.0 spec: {DefaultSpec}", defaultSpec);
                return defaultSpec;
            }
            catch (Exception jex)
            {
                _logger.LogWarning(jex, "Failed to parse JSON from BaseUrl response; defaulting to HSDS-UK 1.0 spec: {DefaultSpec}", defaultSpec);
                return defaultSpec;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error requesting BaseUrl to discover openapi_url; defaulting to HSDS-UK 1.0 spec: {DefaultSpec}", defaultSpec);
            return defaultSpec;
        }
    }

    private static float? ExtractVersionNumber(string version)
    {
        // Try to extract version number from formats like "HSDS-UK-3.0", "V3", "3.0", "3.1", etc.
        var versionString = version
            .Replace("HSDS-UK-", "", StringComparison.OrdinalIgnoreCase)
            .Replace("V", "", StringComparison.OrdinalIgnoreCase)
            .Replace("v", "")
            .Trim();

        if (float.TryParse(versionString, out var versionNumber))
        {
            return versionNumber;
        }

        return null;
    }
}
