using Microsoft.Extensions.Logging;

namespace OpenReferralApi.Core.Logging
{
    public static partial class RemoteSchemaLoaderLog
    {
        [LoggerMessage(EventId = 14000, Level = LogLevel.Debug, Message = "Retrieved schema from cache: {SchemaUrl}")]
        public static partial void RetrievedSchemaFromCache(this ILogger logger, string schemaUrl);

        [LoggerMessage(EventId = 14001, Level = LogLevel.Debug, Message = "Fetching remote schema: {SchemaUrl}")]
        public static partial void FetchingRemoteSchema(this ILogger logger, string schemaUrl);

        [LoggerMessage(EventId = 14002, Level = LogLevel.Debug, Message = "Cached schema: {SchemaUrl} (expires in {Minutes} minutes)")]
        public static partial void CachedSchema(this ILogger logger, string schemaUrl, double minutes);

        [LoggerMessage(EventId = 14003, Level = LogLevel.Error, Message = "Failed to fetch remote schema: {SchemaUrl}")]
        public static partial void FailedToFetchRemoteSchema(this ILogger logger, Exception exception, string schemaUrl);

        [LoggerMessage(EventId = 14011, Level = LogLevel.Warning, Message = "Failed to fetch remote schema from {SchemaUrl} due to connection or timeout issue: {Message}")]
        public static partial void ConnectionFailureFetchingRemoteSchema(this ILogger logger, Exception exception, string schemaUrl, string message);

        [LoggerMessage(EventId = 14004, Level = LogLevel.Warning, Message = "Invalid API key header name provided, skipping API key authentication")]
        public static partial void InvalidApiKeyHeaderName(this ILogger logger);

        [LoggerMessage(EventId = 14005, Level = LogLevel.Debug, Message = "Applied API Key authentication")]
        public static partial void AppliedApiKeyAuthentication(this ILogger logger);

        [LoggerMessage(EventId = 14006, Level = LogLevel.Debug, Message = "Applied Bearer Token authentication")]
        public static partial void AppliedBearerTokenAuthentication(this ILogger logger);

        [LoggerMessage(EventId = 14007, Level = LogLevel.Debug, Message = "Applied Basic authentication")]
        public static partial void AppliedBasicAuthentication(this ILogger logger);

        [LoggerMessage(EventId = 14008, Level = LogLevel.Warning, Message = "Invalid custom header name provided: {HeaderName}")]
        public static partial void InvalidCustomHeaderName(this ILogger logger, string headerName);

        [LoggerMessage(EventId = 14009, Level = LogLevel.Debug, Message = "Applied custom header: {HeaderName}")]
        public static partial void AppliedCustomHeader(this ILogger logger, string headerName);

        [LoggerMessage(EventId = 14010, Level = LogLevel.Warning, Message = "Encountered json-schema.org draft URL not present in configured known schema list: {SchemaUrl}")]
        public static partial void UnknownJsonSchemaDraftUrl(this ILogger logger, string schemaUrl);
    }
}
