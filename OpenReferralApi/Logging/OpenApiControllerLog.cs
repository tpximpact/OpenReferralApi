namespace OpenReferralApi.Logging
{
    internal static partial class OpenApiControllerLog
    {
        [LoggerMessage(EventId = 20000, Level = LogLevel.Information, Message = "Received OpenAPI validation request for BaseUrl: {BaseUrl}")]
        public static partial void ReceivedValidationRequest(this ILogger logger, string baseUrl);

        [LoggerMessage(EventId = 20001, Level = LogLevel.Information, Message = "Validation completed for BaseUrl: {BaseUrl}")]
        public static partial void ValidationCompleted(this ILogger logger, string baseUrl);
    }
}
