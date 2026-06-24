namespace OpenReferralApi.Logging
{
    internal static partial class FeedValidationControllerLog
    {
        [LoggerMessage(EventId = 19000, Level = LogLevel.Information, Message = "Manual validation triggered for all feeds")]
        public static partial void ManualValidationTriggeredForAllFeeds(this ILogger logger);

        [LoggerMessage(EventId = 19001, Level = LogLevel.Information, Message = "Manual validation completed: {Total} feeds, {Up} up, {Valid} valid")]
        public static partial void ManualValidationCompleted(this ILogger logger, int total, int up, int valid);

        [LoggerMessage(EventId = 19002, Level = LogLevel.Information, Message = "Manual validation triggered for feed {FeedId}")]
        public static partial void ManualValidationTriggeredForFeed(this ILogger logger, string feedId);
    }
}
