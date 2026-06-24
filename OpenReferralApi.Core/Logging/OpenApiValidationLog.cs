using Microsoft.Extensions.Logging;

namespace OpenReferralApi.Core.Logging
{
    public static partial class OpenApiValidationLog
    {
        [LoggerMessage(EventId = 18000, Level = LogLevel.Information, Message = "Starting OpenAPI specification testing")]
        public static partial void StartingOpenApiTesting(this ILogger logger);

        [LoggerMessage(EventId = 18001, Level = LogLevel.Information, Message = "No OpenAPI spec found on data service; using profile schema URL {ProfileSchemaUrl} for profile version {ProfileVersion}")]
        public static partial void UsingProfileSchemaUrl(this ILogger logger, string profileSchemaUrl, string profileVersion);

        [LoggerMessage(EventId = 18002, Level = LogLevel.Information, Message = "Discovered OpenAPI schema URL: {Url} (Reason: {Reason})")]
        public static partial void DiscoveredOpenApiSchemaUrl(this ILogger logger, string url, string reason);

        [LoggerMessage(EventId = 18003, Level = LogLevel.Information, Message = "OpenAPI schema URL discovery failed for base URL {BaseUrl}; using configured default profile URL {ProfileSchemaUrl}")]
        public static partial void UsingDefaultProfileSchemaUrl(this ILogger logger, string baseUrl, string profileSchemaUrl);

        [LoggerMessage(EventId = 18004, Level = LogLevel.Information, Message = "OpenAPI testing completed. IsValid: {IsValid}, Endpoints: {EndpointCount}")]
        public static partial void OpenApiTestingCompleted(this ILogger logger, bool isValid, int endpointCount);

        [LoggerMessage(EventId = 18005, Level = LogLevel.Error, Message = "Error during OpenAPI testing")]
        public static partial void ErrorDuringOpenApiTesting(this ILogger logger, Exception exception);

        [LoggerMessage(EventId = 18006, Level = LogLevel.Warning, Message = "Specification.DefaultProfileVersion '{DefaultProfileVersion}' is not a valid key in Specification.Urls. Falling back to existing behavior.")]
        public static partial void InvalidDefaultProfileVersion(this ILogger logger, string defaultProfileVersion);

        [LoggerMessage(EventId = 18007, Level = LogLevel.Debug, Message = "Resolved OpenAPI cache hit (scope: {CacheScope}) for URL {SpecUrl}")]
        public static partial void ResolvedOpenApiCacheHit(this ILogger logger, string cacheScope, string specUrl);

        [LoggerMessage(EventId = 18008, Level = LogLevel.Debug, Message = "Resolved OpenAPI cache miss (scope: {CacheScope}) for URL {SpecUrl}")]
        public static partial void ResolvedOpenApiCacheMiss(this ILogger logger, string cacheScope, string specUrl);

        [LoggerMessage(EventId = 18009, Level = LogLevel.Debug, Message = "Resolved HSDS profile OpenAPI via schema resolver warmup path for URL {SpecUrl}")]
        public static partial void ResolvedProfileViaWarmup(this ILogger logger, string specUrl);

        [LoggerMessage(EventId = 18010, Level = LogLevel.Debug, Message = "Warmup-path resolution unavailable for HSDS profile URL {SpecUrl}; falling back to direct fetch")]
        public static partial void WarmupPathResolutionUnavailable(this ILogger logger, Exception exception, string specUrl);

        [LoggerMessage(EventId = 18011, Level = LogLevel.Warning, Message = "Failed to fetch/resolve OpenAPI from feed URL {FeedSpecUrl}; falling back to HSDS profile schema {ProfileSchemaUrl}")]
        public static partial void FallingBackToHsdsProfileSchema(this ILogger logger, Exception exception, string feedSpecUrl, string profileSchemaUrl);

        [LoggerMessage(EventId = 18012, Level = LogLevel.Debug, Message = "Configured default HSDS profile fallback could not be resolved from URL {ProfileSchemaUrl}")]
        public static partial void DefaultProfileFallbackCouldNotBeResolved(this ILogger logger, Exception exception, string profileSchemaUrl);

        [LoggerMessage(EventId = 18013, Level = LogLevel.Warning, Message = "HSDS schema version was incorrectly defined in the 'openapi' field (value: {OpenapiValue}). The 'openapi' field specifies the OpenAPI specification version, not the HSDS schema version. Detected HSDS version {HsdsVersion} — please add an 'x-hsds-version' or 'version' field to the spec.")]
        public static partial void HsdsVersionMisplaced(this ILogger logger, string openapiValue, string hsdsVersion);

        [LoggerMessage(EventId = 18015, Level = LogLevel.Information, Message = "OpenAPI validation run memory usage at completion. ManagedHeapBytes: {ManagedHeapBytes}, ProcessWorkingSetBytes: {ProcessWorkingSetBytes}, DurationMs: {DurationMs}")]
        public static partial void OpenApiValidationMemoryUsageAtCompletion(this ILogger logger, long managedHeapBytes, long processWorkingSetBytes, double durationMs);

        [LoggerMessage(EventId = 18016, Level = LogLevel.Information, Message = "Resolved OpenAPI cache state at stage {Stage}. FeedEntries: {FeedEntries}, ProfileEntries: {ProfileEntries}, ExpiredEntries: {ExpiredEntries}, FeedJsonChars: {FeedJsonChars}, ProfileJsonChars: {ProfileJsonChars}")]
        public static partial void ResolvedOpenApiCacheState(this ILogger logger, string stage, int feedEntries, int profileEntries, int expiredEntries, long feedJsonChars, long profileJsonChars);

        [LoggerMessage(EventId = 18017, Level = LogLevel.Information, Message = "Resolved OpenAPI cache lookup {Outcome} (scope: {CacheScope}) for URL {SpecUrl}. ManagedHeapBytes: {ManagedHeapBytes}, ManagedHeapDeltaBytes: {ManagedHeapDeltaBytes}, ProcessWorkingSetBytes: {ProcessWorkingSetBytes}, ProcessWorkingSetDeltaBytes: {ProcessWorkingSetDeltaBytes}, ElapsedMs: {ElapsedMs}")]
        public static partial void ResolvedOpenApiCacheLookupMemoryCheckpoint(this ILogger logger, string outcome, string cacheScope, string specUrl, long managedHeapBytes, long managedHeapDeltaBytes, long processWorkingSetBytes, long processWorkingSetDeltaBytes, double elapsedMs);
    }
}
