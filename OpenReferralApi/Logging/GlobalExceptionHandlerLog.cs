namespace OpenReferralApi.Logging
{
    internal static partial class GlobalExceptionHandlerLog
    {
        [LoggerMessage(EventId = 22000, Level = LogLevel.Error, Message = "An unhandled exception occurred. TraceId: {TraceId}")]
        public static partial void UnhandledExceptionOccurred(this ILogger logger, Exception exception, string traceId);
    }
}
