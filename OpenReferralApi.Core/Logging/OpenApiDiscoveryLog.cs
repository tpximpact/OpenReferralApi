using Microsoft.Extensions.Logging;

namespace OpenReferralApi.Core.Logging
{
    public static partial class OpenApiDiscoveryLog
    {
        [LoggerMessage(EventId = 8000, Level = LogLevel.Debug, Message = "Probing standard path: {SpecUrl}")]
        public static partial void ProbingStandardPath(this ILogger logger, string specUrl);

        [LoggerMessage(EventId = 8001, Level = LogLevel.Information, Message = "Discovered feed OpenAPI spec via probing at {Path}")]
        public static partial void DiscoveredSpecViaProbing(this ILogger logger, string path);

        [LoggerMessage(EventId = 8002, Level = LogLevel.Debug, Message = "Path {Path} returned 200 but content does not look like an OpenAPI spec")]
        public static partial void PathReturnedNonOpenApiContent(this ILogger logger, string path);

        [LoggerMessage(EventId = 8003, Level = LogLevel.Debug, Message = "Path {Path} returned {StatusCode}")]
        public static partial void PathReturnedStatusCode(this ILogger logger, string path, int statusCode);

        [LoggerMessage(EventId = 8004, Level = LogLevel.Debug, Message = "Probe failed for {BaseUrl}{Path}")]
        public static partial void ProbeFailed(this ILogger logger, Exception exception, string baseUrl, string path);

        [LoggerMessage(EventId = 8005, Level = LogLevel.Debug, Message = "Probing swagger-config path: {ConfigUrl}")]
        public static partial void ProbingSwaggerConfigPath(this ILogger logger, string configUrl);

        [LoggerMessage(EventId = 8006, Level = LogLevel.Information, Message = "Discovered feed OpenAPI spec via config endpoint: {SpecUrl}")]
        public static partial void DiscoveredSpecViaConfigEndpoint(this ILogger logger, string specUrl);

        [LoggerMessage(EventId = 8007, Level = LogLevel.Debug, Message = "No definitions found at config path {ConfigPath}")]
        public static partial void NoDefinitionsAtConfigPath(this ILogger logger, string configPath);

        [LoggerMessage(EventId = 8008, Level = LogLevel.Debug, Message = "Config probe failed for {ConfigPath}")]
        public static partial void ConfigProbeFailed(this ILogger logger, Exception exception, string configPath);

        [LoggerMessage(EventId = 8009, Level = LogLevel.Information, Message = "Discovered feed OpenAPI spec via HTML scraping: {SpecUrl}")]
        public static partial void DiscoveredSpecViaHtmlScraping(this ILogger logger, string specUrl);

        [LoggerMessage(EventId = 8010, Level = LogLevel.Warning, Message = "HTML scraping failed for base URL {BaseUrl}")]
        public static partial void HtmlScrapingFailed(this ILogger logger, Exception exception, string baseUrl);

        [LoggerMessage(EventId = 8011, Level = LogLevel.Debug, Message = "Probing UI route: {UiUrl}")]
        public static partial void ProbingUiRoute(this ILogger logger, string uiUrl);

        [LoggerMessage(EventId = 8012, Level = LogLevel.Debug, Message = "UI route {UiPath} returned {StatusCode}")]
        public static partial void UiRouteReturnedStatusCode(this ILogger logger, string uiPath, int statusCode);

        [LoggerMessage(EventId = 8013, Level = LogLevel.Information, Message = "Discovered feed OpenAPI spec via UI route scraping: {SpecUrl}")]
        public static partial void DiscoveredSpecViaUiRouteScraping(this ILogger logger, string specUrl);

        [LoggerMessage(EventId = 8014, Level = LogLevel.Debug, Message = "UI route {UiPath} returned 200 but no OpenAPI spec URL found in HTML")]
        public static partial void UiRouteNoSpecUrlFound(this ILogger logger, string uiPath);

        [LoggerMessage(EventId = 8015, Level = LogLevel.Debug, Message = "UI probe failed for {UiUrl}")]
        public static partial void UiProbeFailed(this ILogger logger, Exception exception, string uiUrl);

        [LoggerMessage(EventId = 8016, Level = LogLevel.Debug, Message = "Discovered spec URL {SpecUrl} returned {StatusCode} while trying to fetch spec content")]
        public static partial void DiscoveredSpecUrlReturnedStatusCode(this ILogger logger, string specUrl, int statusCode);

        [LoggerMessage(EventId = 8017, Level = LogLevel.Debug, Message = "Failed to fetch discovered spec content for {SpecUrl}")]
        public static partial void FailedToFetchDiscoveredSpecContent(this ILogger logger, Exception exception, string specUrl);

        [LoggerMessage(EventId = 8018, Level = LogLevel.Debug, Message = "Discovered OpenAPI candidate from UI HTML definition: {SpecUrl}")]
        public static partial void DiscoveredCandidateFromUiHtml(this ILogger logger, string specUrl);

        [LoggerMessage(EventId = 8019, Level = LogLevel.Debug, Message = "Discovered Swagger config candidate from UI HTML: {ConfigUrl}")]
        public static partial void DiscoveredSwaggerConfigCandidate(this ILogger logger, string configUrl);

        [LoggerMessage(EventId = 8020, Level = LogLevel.Debug, Message = "Requesting swagger-config endpoint: {ConfigUrl}")]
        public static partial void RequestingSwaggerConfigEndpoint(this ILogger logger, string configUrl);

        [LoggerMessage(EventId = 8021, Level = LogLevel.Debug, Message = "Swagger-config endpoint {ConfigUrl} returned {StatusCode}")]
        public static partial void SwaggerConfigEndpointReturnedStatusCode(this ILogger logger, string configUrl, int statusCode);

        [LoggerMessage(EventId = 8022, Level = LogLevel.Debug, Message = "Discovered OpenAPI candidate from swagger-config endpoint {ConfigUrl}: {SpecUrl}")]
        public static partial void DiscoveredCandidateFromSwaggerConfig(this ILogger logger, string configUrl, string specUrl);

        [LoggerMessage(EventId = 8023, Level = LogLevel.Information, Message = "Unable to discover OpenAPI spec from base URL {BaseUrl} after exhaustive probing of standard, config, and UI paths")]
        public static partial void UnableToDiscoverOpenApiSpec(this ILogger logger, string baseUrl);
    }
}
