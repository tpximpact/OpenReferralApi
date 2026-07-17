using Microsoft.Extensions.Logging;

namespace OpenReferralApi.Core.Logging
{
    public static partial class OpenApiSpecFetcherLog
    {
        [LoggerMessage(EventId = 9000, Level = LogLevel.Warning, Message = "User-supplied authentication was provided but is disabled by server configuration. Skipping authentication headers for request to {SpecUrl}")]
        public static partial void AuthDisabledByServerConfig(this ILogger logger, string specUrl);

        [LoggerMessage(EventId = 9001, Level = LogLevel.Error, Message = "Refusing to send authentication credentials over a non-HTTPS connection to {SpecUrl}")]
        public static partial void RefusingNonHttpsAuth(this ILogger logger, string specUrl);

        [LoggerMessage(EventId = 9002, Level = LogLevel.Information, Message = "Fetching OpenAPI specification from URL: {SpecUrl}")]
        public static partial void FetchingOpenApiSpec(this ILogger logger, string specUrl);

        [LoggerMessage(EventId = 9003, Level = LogLevel.Warning, Message = "User-supplied authentication was provided but is disabled by server configuration. Skipping authentication headers for request to {SpecUrl}")]
        public static partial void AuthDisabledForRequest(this ILogger logger, string specUrl);

        [LoggerMessage(EventId = 9004, Level = LogLevel.Error, Message = "Failed to fetch OpenAPI specification from URL: {SpecUrl}")]
        public static partial void FailedToFetchOpenApiSpec(this ILogger logger, Exception exception, string specUrl);

        [LoggerMessage(EventId = 9005, Level = LogLevel.Debug, Message = "Applied API Key authentication with header: {Header}")]
        public static partial void AppliedApiKeyAuthentication(this ILogger logger, string header);

        [LoggerMessage(EventId = 9006, Level = LogLevel.Debug, Message = "Applied Bearer Token authentication")]
        public static partial void AppliedBearerTokenAuthentication(this ILogger logger);

        [LoggerMessage(EventId = 9007, Level = LogLevel.Debug, Message = "Applied Basic authentication")]
        public static partial void AppliedBasicAuthentication(this ILogger logger);

        [LoggerMessage(EventId = 9008, Level = LogLevel.Debug, Message = "Applied custom header: {HeaderName}")]
        public static partial void AppliedCustomHeader(this ILogger logger, string headerName);
    }
}
