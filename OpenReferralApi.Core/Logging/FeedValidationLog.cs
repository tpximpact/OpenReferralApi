using Microsoft.Extensions.Logging;

namespace OpenReferralApi.Core.Logging
{
    public static partial class FeedValidationLog
    {
        [LoggerMessage(EventId = 2000, Level = LogLevel.Information, Message = "Starting scheduled feed validation run at {Time}")]
        public static partial void ScheduledValidationStarted(this ILogger logger, DateTime time);

        [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "Completed scheduled feed validation run at {Time}")]
        public static partial void ScheduledValidationCompleted(this ILogger logger, DateTime time);

        [LoggerMessage(EventId = 2002, Level = LogLevel.Error, Message = "Error during scheduled feed validation")]
        public static partial void ScheduledValidationError(this ILogger logger, Exception exception);

        [LoggerMessage(EventId = 2003, Level = LogLevel.Warning, Message = "No feeds found in database")]
        public static partial void NoFeedsFound(this ILogger logger);
    }
}
