using Microsoft.Extensions.Logging;

namespace OpenReferralApi.Core.Logging
{
    public static partial class JsonValidatorLog
    {
        [LoggerMessage(EventId = 6000, Level = LogLevel.Information, Message = "Starting JSON validation for request")]
        public static partial void StartingJsonValidation(this ILogger logger);

        [LoggerMessage(EventId = 6001, Level = LogLevel.Information, Message = "JSON validation completed. IsValid: {IsValid}, Errors: {ErrorCount}")]
        public static partial void JsonValidationCompleted(this ILogger logger, bool isValid, int errorCount);

        [LoggerMessage(EventId = 6002, Level = LogLevel.Error, Message = "Invalid argument during JSON validation")]
        public static partial void InvalidArgumentDuringJsonValidation(this ILogger logger, Exception exception);

        [LoggerMessage(EventId = 6003, Level = LogLevel.Error, Message = "Invalid operation during JSON validation")]
        public static partial void InvalidOperationDuringJsonValidation(this ILogger logger, Exception exception);

        [LoggerMessage(EventId = 6004, Level = LogLevel.Error, Message = "Unexpected error during JSON validation")]
        public static partial void UnexpectedErrorDuringJsonValidation(this ILogger logger, Exception exception);

        [LoggerMessage(EventId = 6005, Level = LogLevel.Information, Message = "Starting schema validation")]
        public static partial void StartingSchemaValidation(this ILogger logger);

        [LoggerMessage(EventId = 6006, Level = LogLevel.Information, Message = "Schema validation completed. IsValid: {IsValid}")]
        public static partial void SchemaValidationCompleted(this ILogger logger, bool isValid);

        [LoggerMessage(EventId = 6007, Level = LogLevel.Error, Message = "Error during schema validation")]
        public static partial void ErrorDuringSchemaValidation(this ILogger logger, Exception exception);

        [LoggerMessage(EventId = 6008, Level = LogLevel.Error, Message = "Failed to load schema from URI: {SchemaUri}")]
        public static partial void FailedToLoadSchemaFromUri(this ILogger logger, Exception exception, string schemaUri);

        [LoggerMessage(EventId = 6009, Level = LogLevel.Debug, Message = "Using cached schema document for URI: {SchemaUri}")]
        public static partial void UsingCachedSchemaDocument(this ILogger logger, string schemaUri);

        [LoggerMessage(EventId = 6010, Level = LogLevel.Information, Message = "Loading schema from URI: {SchemaUri}")]
        public static partial void LoadingSchemaFromUri(this ILogger logger, string schemaUri);

        [LoggerMessage(EventId = 6011, Level = LogLevel.Error, Message = "Failed to load schema from URI: {SchemaUri}")]
        public static partial void FailedToLoadSchemaFromUriRetry(this ILogger logger, Exception exception, string schemaUri);

        [LoggerMessage(EventId = 6012, Level = LogLevel.Error, Message = "Failed to create schema from object")]
        public static partial void FailedToCreateSchemaFromObject(this ILogger logger, Exception exception);

        [LoggerMessage(EventId = 6013, Level = LogLevel.Information, Message = "Fetching JSON data from URL: {DataUrl}")]
        public static partial void FetchingJsonDataFromUrl(this ILogger logger, string dataUrl);

        [LoggerMessage(EventId = 6014, Level = LogLevel.Error, Message = "HTTP request failed when fetching data from URL: {DataUrl}")]
        public static partial void HttpRequestFailedFetchingData(this ILogger logger, Exception exception, string dataUrl);

        [LoggerMessage(EventId = 6015, Level = LogLevel.Error, Message = "Invalid JSON received from URL: {DataUrl}")]
        public static partial void InvalidJsonReceived(this ILogger logger, Exception exception, string dataUrl);

        [LoggerMessage(EventId = 6016, Level = LogLevel.Debug, Message = "Failed to extract title from original schema object")]
        public static partial void FailedToExtractTitleFromOriginalSchema(this ILogger logger, Exception exception);

        [LoggerMessage(EventId = 6017, Level = LogLevel.Debug, Message = "Failed to extract description from original schema object")]
        public static partial void FailedToExtractDescriptionFromOriginalSchema(this ILogger logger, Exception exception);

        [LoggerMessage(EventId = 6018, Level = LogLevel.Debug, Message = "Failed to extract title from schema object")]
        public static partial void FailedToExtractTitleFromSchema(this ILogger logger, Exception exception);

        [LoggerMessage(EventId = 6019, Level = LogLevel.Debug, Message = "Failed to extract description from schema object")]
        public static partial void FailedToExtractDescriptionFromSchema(this ILogger logger, Exception exception);

        [LoggerMessage(EventId = 6020, Level = LogLevel.Warning, Message = "Error detecting additional fields")]
        public static partial void ErrorDetectingAdditionalFields(this ILogger logger, Exception exception);

        [LoggerMessage(EventId = 6021, Level = LogLevel.Warning, Message = "User JSON failed validation due to circular reference or depth > {MaxDepth}. Source: {SourceIdentifier}")]
        public static partial void UserJsonCycleOrDepthLimitExceeded(this ILogger logger, Exception exception, int maxDepth, string sourceIdentifier);

        [LoggerMessage(EventId = 6022, Level = LogLevel.Error, Message = "Cached schema at URI {SchemaUri} failed validation due to circular reference or depth > {MaxDepth}")]
        public static partial void CachedSchemaCycleOrDepthLimitExceeded(this ILogger logger, Exception exception, string schemaUri, int maxDepth);

        [LoggerMessage(EventId = 6023, Level = LogLevel.Warning, Message = "User provided schema failed validation due to circular reference or depth > {MaxDepth}. Source: {SourceIdentifier}")]
        public static partial void UserSchemaCycleOrDepthLimitExceeded(this ILogger logger, Exception exception, int maxDepth, string sourceIdentifier);

        [LoggerMessage(EventId = 6024, Level = LogLevel.Information, Message = "Using pre-registered schema for URI: {SchemaUri}")]
        public static partial void UsingPreRegisteredSchema(this ILogger logger, string schemaUri);
    }
}
