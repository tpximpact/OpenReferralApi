using System;
using Microsoft.Extensions.Logging;

namespace OpenReferralApi.Core.Logging
{
    public static partial class PathParsingLog
    {
        [LoggerMessage(EventId = 11000, Level = LogLevel.Debug, Message = "Checking accessibility of URI: {Uri}")]
        public static partial void CheckingAccessibilityOfUri(this ILogger logger, Uri uri);

        [LoggerMessage(EventId = 11001, Level = LogLevel.Warning, Message = "HTTP request failed for URI: {Uri}")]
        public static partial void HttpRequestFailedForUri(this ILogger logger, Exception exception, Uri uri);

        [LoggerMessage(EventId = 11002, Level = LogLevel.Warning, Message = "Request timeout for URI: {Uri}")]
        public static partial void RequestTimeoutForUri(this ILogger logger, Uri uri);

        [LoggerMessage(EventId = 11003, Level = LogLevel.Error, Message = "Error checking accessibility of URI: {Uri}")]
        public static partial void ErrorCheckingAccessibilityOfUri(this ILogger logger, Exception exception, Uri uri);

        [LoggerMessage(EventId = 11004, Level = LogLevel.Error, Message = "Error resolving relative URI '{RelativeUri}' against base '{BaseUrl}'")]
        public static partial void ErrorResolvingRelativeUri(this ILogger logger, Exception exception, string relativeUri, Uri? baseUrl);

        [LoggerMessage(EventId = 11005, Level = LogLevel.Debug, Message = "Validating {UriType}: {Uri}")]
        public static partial void ValidatingUri(this ILogger logger, string uriType, string uri);

        [LoggerMessage(EventId = 11006, Level = LogLevel.Debug, Message = "Successfully validated {UriType}: {Uri}")]
        public static partial void SuccessfullyValidatedUri(this ILogger logger, string uriType, Uri uri);

        [LoggerMessage(EventId = 11007, Level = LogLevel.Error, Message = "URI format error for {UriType}: {Uri}")]
        public static partial void UriFormatError(this ILogger logger, Exception exception, string uriType, string uri);

        [LoggerMessage(EventId = 11008, Level = LogLevel.Error, Message = "Error validating {UriType}: {Uri}")]
        public static partial void ErrorValidatingUri(this ILogger logger, Exception exception, string uriType, string uri);

        [LoggerMessage(EventId = 11009, Level = LogLevel.Debug, Message = "SSL certificate validation enabled for {UriType}: {Uri}")]
        public static partial void SslCertificateValidationEnabled(this ILogger logger, string uriType, Uri uri);

        [LoggerMessage(EventId = 11010, Level = LogLevel.Warning, Message = "Potentially unsafe {UriType} accessing private/localhost: {Uri}")]
        public static partial void PotentiallyUnsafeUri(this ILogger logger, string uriType, Uri uri);
    }
}
