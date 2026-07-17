using Microsoft.Extensions.Logging;

namespace OpenReferralApi.Core.Logging
{
    public static partial class SchemaResolverLog
    {
        [LoggerMessage(EventId = 16000, Level = LogLevel.Debug, Message = "Creating JSON schema from JSON string with resolver. DocumentUri: {DocumentUri}")]
        public static partial void CreatingJsonSchema(this ILogger logger, string documentUri);

        [LoggerMessage(EventId = 16001, Level = LogLevel.Debug, Message = "Pre-resolving all schema references with base URI: {DocumentUri}")]
        public static partial void PreResolvingSchemaReferences(this ILogger logger, string documentUri);

        [LoggerMessage(EventId = 16002, Level = LogLevel.Debug, Message = "Successfully pre-resolved all schema references")]
        public static partial void SuccessfullyPreResolvedSchemaReferences(this ILogger logger);

        [LoggerMessage(EventId = 16003, Level = LogLevel.Warning, Message = "Failed to pre-resolve schema, continuing with original schema")]
        public static partial void FailedToPreResolveSchema(this ILogger logger, Exception exception);

        [LoggerMessage(EventId = 16004, Level = LogLevel.Debug, Message = "Loading schema with base URI: {DocumentUri}")]
        public static partial void LoadingSchemaWithBaseUri(this ILogger logger, string documentUri);

        [LoggerMessage(EventId = 16005, Level = LogLevel.Debug, Message = "Successfully created schema with reference resolution")]
        public static partial void SuccessfullyCreatedSchemaWithReferenceResolution(this ILogger logger);

        [LoggerMessage(EventId = 16006, Level = LogLevel.Warning, Message = "Failed to parse schema with resolver, attempting to parse without resolver. DocumentUri: {DocumentUri}")]
        public static partial void FailedToParseSchemaWithResolver(this ILogger logger, Exception exception, string documentUri);

        [LoggerMessage(EventId = 16007, Level = LogLevel.Debug, Message = "Successfully created schema without resolver")]
        public static partial void SuccessfullyCreatedSchemaWithoutResolver(this ILogger logger);

        [LoggerMessage(EventId = 16008, Level = LogLevel.Error, Message = "Failed to parse schema even without resolver. DocumentUri: {DocumentUri}. ReaderError: {ReaderError}. OriginalFingerprint: {OriginalFingerprint}. ResolvedFingerprint: {ResolvedFingerprint}. OriginalSchemaId: {OriginalSchemaId}. ResolvedSchemaId: {ResolvedSchemaId}")]
        public static partial void FailedToParseSchemaWithoutResolver(this ILogger logger, Exception exception, string documentUri, string readerError, string originalFingerprint, string resolvedFingerprint, string originalSchemaId, string resolvedSchemaId);

        [LoggerMessage(EventId = 16009, Level = LogLevel.Error, Message = "Failed to create JSON schema from JSON with resolver. DocumentUri: {DocumentUri}")]
        public static partial void FailedToCreateJsonSchema(this ILogger logger, Exception exception, string documentUri);

        [LoggerMessage(EventId = 16010, Level = LogLevel.Warning, Message = "Failed to pre-fetch schema ref: {Url}")]
        public static partial void FailedToPreFetchSchemaRef(this ILogger logger, Exception exception, string url);
    }
}
