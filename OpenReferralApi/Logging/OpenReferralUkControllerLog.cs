namespace OpenReferralApi.Logging
{
    internal static partial class OpenReferralUkControllerLog
    {
        [LoggerMessage(EventId = 21000, Level = LogLevel.Information, Message = "Received OpenAPI validation request (Open Referral UK format) for BaseUrl: {BaseUrl}")]
        public static partial void ReceivedValidationRequest(this ILogger logger, string baseUrl);

        [LoggerMessage(EventId = 21001, Level = LogLevel.Information, Message = "Validation completed (Open Referral UK format) for BaseUrl: {BaseUrl}")]
        public static partial void ValidationCompleted(this ILogger logger, string baseUrl);
    }
}
