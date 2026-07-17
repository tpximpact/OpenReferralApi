namespace OpenReferralApi.Logging
{
    internal static partial class FeedValidationBackgroundLog
    {
        [LoggerMessage(EventId = 23000, Level = LogLevel.Information, Message = "Feed Validation Background Service is disabled. Set FeedValidation:Enabled=true to enable.")]
        public static partial void ServiceDisabled(this ILogger logger);

        [LoggerMessage(EventId = 23001, Level = LogLevel.Information, Message = "Feed Validation Background Service started. Interval: {Interval} hours, RunAtMidnight: {RunAtMidnight}")]
        public static partial void ServiceStarted(this ILogger logger, double interval, bool runAtMidnight);

        [LoggerMessage(EventId = 23002, Level = LogLevel.Information, Message = "Next validation scheduled for {NextRun} (in {Hours} hours)")]
        public static partial void NextValidationScheduledForMidnight(this ILogger logger, DateTime nextRun, double hours);

        [LoggerMessage(EventId = 23003, Level = LogLevel.Information, Message = "Next validation scheduled in {Hours} hours")]
        public static partial void NextValidationScheduled(this ILogger logger, double hours);

        [LoggerMessage(EventId = 23004, Level = LogLevel.Information, Message = "Feed validation service is stopping")]
        public static partial void ServiceStopping(this ILogger logger);

        [LoggerMessage(EventId = 23005, Level = LogLevel.Information, Message = "Found {FeedCount} registered feeds to validate")]
        public static partial void FoundFeedsToValidate(this ILogger logger, int feedCount);

        [LoggerMessage(EventId = 23006, Level = LogLevel.Information, Message = "Feed validation summary: Total={Total}, Up={Up}, Valid={Valid}, Down={Down}, Duration={Duration}s")]
        public static partial void FeedValidationSummary(this ILogger logger, int total, int up, int valid, int down, double duration);

        [LoggerMessage(EventId = 23007, Level = LogLevel.Error, Message = "Error validating feeds")]
        public static partial void ErrorValidatingFeeds(this ILogger logger, Exception exception);

        [LoggerMessage(EventId = 23008, Level = LogLevel.Information, Message = "Feed Validation Background Service is stopping")]
        public static partial void BackgroundServiceStopping(this ILogger logger);
    }
}
