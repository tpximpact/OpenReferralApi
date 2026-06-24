using Microsoft.Extensions.Logging;

namespace OpenReferralApi.Core.Logging
{
    public static partial class FeedValidationServiceLog
    {
        [LoggerMessage(EventId = 17000, Level = LogLevel.Error, Message = "Failed to retrieve feeds from database")]
        public static partial void FailedToRetrieveFeeds(this ILogger logger, Exception exception);

        [LoggerMessage(EventId = 17001, Level = LogLevel.Warning, Message = "Feed {FeedId} not found for update")]
        public static partial void FeedNotFoundForUpdate(this ILogger logger, string feedId);

        [LoggerMessage(EventId = 17002, Level = LogLevel.Information, Message = "Updated feed {FeedId}: IsUp={IsUp}, IsValid={IsValid}, ResponseTime={ResponseTime}ms, Errors={ErrorCount}")]
        public static partial void FeedStatusUpdated(this ILogger logger, string feedId, bool isUp, bool isValid, double? responseTime, int? errorCount);

        [LoggerMessage(EventId = 17003, Level = LogLevel.Error, Message = "Failed to update feed status for feed {FeedId}")]
        public static partial void FailedToUpdateFeedStatus(this ILogger logger, Exception exception, string feedId);

        [LoggerMessage(EventId = 17004, Level = LogLevel.Information, Message = "Validating feed: {FeedName} ({FeedUrl})")]
        public static partial void ValidatingFeed(this ILogger logger, string feedName, string feedUrl);

        [LoggerMessage(EventId = 17005, Level = LogLevel.Information, Message = "Feed validation completed: {FeedName} - IsUp={IsUp}, IsValid={IsValid}, Errors={ErrorCount}")]
        public static partial void FeedValidationCompleted(this ILogger logger, string feedName, bool isUp, bool isValid, int errorCount);

        [LoggerMessage(EventId = 17006, Level = LogLevel.Warning, Message = "Feed is not accessible: {FeedUrl}")]
        public static partial void FeedNotAccessible(this ILogger logger, Exception exception, string feedUrl);

        [LoggerMessage(EventId = 17007, Level = LogLevel.Warning, Message = "Feed validation timed out: {FeedUrl}")]
        public static partial void FeedValidationTimedOut(this ILogger logger, Exception exception, string feedUrl);

        [LoggerMessage(EventId = 17008, Level = LogLevel.Error, Message = "Unexpected error validating feed: {FeedUrl}")]
        public static partial void UnexpectedErrorValidatingFeed(this ILogger logger, Exception exception, string feedUrl);

        [LoggerMessage(EventId = 17009, Level = LogLevel.Error, Message = "Failed to validate feed {FeedId}")]
        public static partial void FailedToValidateFeed(this ILogger logger, Exception exception, string feedId);

        [LoggerMessage(EventId = 17010, Level = LogLevel.Warning, Message = "Feed validation service is not available. MongoDB is not configured.")]
        public static partial void FeedValidationServiceNotAvailable(this ILogger logger);
    }
}
