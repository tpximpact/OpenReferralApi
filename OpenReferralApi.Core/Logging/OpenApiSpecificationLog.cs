using Microsoft.Extensions.Logging;

namespace OpenReferralApi.Core.Logging
{
    public static partial class OpenApiSpecificationLog
    {
        [LoggerMessage(EventId = 10000, Level = LogLevel.Information, Message = "Validating OpenAPI specification")]
        public static partial void ValidatingOpenApiSpecification(this ILogger logger);

        [LoggerMessage(EventId = 10001, Level = LogLevel.Error, Message = "Error during OpenAPI validation")]
        public static partial void ErrorDuringOpenApiValidation(this ILogger logger, Exception exception);

        [LoggerMessage(EventId = 10002, Level = LogLevel.Debug, Message = "Validated OpenAPI specification {DialogInfo} with schema URI: {SchemaUri}")]
        public static partial void ValidatedOpenApiSpecification(this ILogger logger, string dialogInfo, string schemaUri);

        [LoggerMessage(EventId = 10003, Level = LogLevel.Warning, Message = "Could not validate against OpenAPI schema")]
        public static partial void CouldNotValidateAgainstSchema(this ILogger logger, Exception exception);

        [LoggerMessage(EventId = 10004, Level = LogLevel.Information, Message = "OpenAPI specification validation completed. IsValid: {IsValid}, Errors: {ErrorCount}")]
        public static partial void OpenApiValidationCompleted(this ILogger logger, bool isValid, int errorCount);

        [LoggerMessage(EventId = 10005, Level = LogLevel.Warning, Message = "Error analyzing schema structure")]
        public static partial void ErrorAnalyzingSchemaStructure(this ILogger logger, Exception exception);

        [LoggerMessage(EventId = 10006, Level = LogLevel.Debug, Message = "Error counting examples in specification")]
        public static partial void ErrorCountingExamples(this ILogger logger, Exception exception);

        [LoggerMessage(EventId = 10007, Level = LogLevel.Warning, Message = "Error analyzing quality metrics")]
        public static partial void ErrorAnalyzingQualityMetrics(this ILogger logger, Exception exception);

        [LoggerMessage(EventId = 10008, Level = LogLevel.Warning, Message = "Error generating recommendations")]
        public static partial void ErrorGeneratingRecommendations(this ILogger logger, Exception exception);
    }
}
