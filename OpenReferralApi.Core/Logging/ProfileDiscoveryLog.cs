using Microsoft.Extensions.Logging;

namespace OpenReferralApi.Core.Logging
{
    public static partial class ProfileDiscoveryLog
    {
        [LoggerMessage(EventId = 12000, Level = LogLevel.Information, Message = "Requesting BaseUrl to discover openapi_url: {BaseUrl}")]
        public static partial void RequestingBaseUrl(this ILogger logger, string baseUrl);

        [LoggerMessage(EventId = 12001, Level = LogLevel.Information, Message = "BaseUrl request returned {Status}; unable to determine HSDS schema version")]
        public static partial void BaseUrlRequestFailed(this ILogger logger, int status);

        [LoggerMessage(EventId = 12002, Level = LogLevel.Information, Message = "Discovered openapi_url: {OpenApiUrl}")]
        public static partial void DiscoveredOpenApiUrl(this ILogger logger, string openApiUrl);

        [LoggerMessage(EventId = 12003, Level = LogLevel.Information, Message = "Detected version '{Version}'; resolved spec: {OpenApiUrl}")]
        public static partial void DetectedVersionResolvedSpec(this ILogger logger, string version, string openApiUrl);

        [LoggerMessage(EventId = 12004, Level = LogLevel.Information, Message = "No openapi_url or version in BaseUrl response; unable to determine HSDS schema version")]
        public static partial void NoOpenApiUrlOrVersionFound(this ILogger logger);

        [LoggerMessage(EventId = 12005, Level = LogLevel.Warning, Message = "Failed to parse JSON from BaseUrl response; unable to determine HSDS schema version")]
        public static partial void FailedToParseBaseUrlJson(this ILogger logger, Exception exception);

        [LoggerMessage(EventId = 12006, Level = LogLevel.Warning, Message = "Error requesting BaseUrl to discover openapi_url; unable to determine HSDS schema version")]
        public static partial void ErrorRequestingBaseUrl(this ILogger logger, Exception exception);

       [LoggerMessage(EventId = 7000, Level = LogLevel.Information, Message = "Profile discovery resolved: DiscoverytUrl '{discoveryUrl}'; with profile context '{discoveredVersion}':{ProfileReason}")]
        public static partial void ProfileDiscoveryResolved(this ILogger logger, string discoveryUrl, string discoveredVersion,  string profileReason);

        [LoggerMessage(EventId = 12007, Level = LogLevel.Information, Message = "Profile schema '{ProfileVersion}' not found in cache. Fetching from {SchemaUrl} on demand.")]
        public static partial void ProfileSchemaCacheMiss(this ILogger logger, string profileVersion, string schemaUrl);

        [LoggerMessage(EventId = 12008, Level = LogLevel.Warning, Message = "Failed to fetch profile schema from {SchemaUrl}. Status code: {StatusCode}")]
        public static partial void FailedToFetchProfileSchema(this ILogger logger, string schemaUrl, int statusCode);

        [LoggerMessage(EventId = 12009, Level = LogLevel.Error, Message = "Error fetching profile schema from {SchemaUrl} on demand.")]
        public static partial void ErrorFetchingProfileSchema(this ILogger logger, Exception exception, string schemaUrl);
    }
}
