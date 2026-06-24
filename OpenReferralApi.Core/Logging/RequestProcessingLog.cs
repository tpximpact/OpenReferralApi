using Microsoft.Extensions.Logging;

namespace OpenReferralApi.Core.Logging
{
    public static partial class RequestProcessingLog
    {
        [LoggerMessage(EventId = 15000, Level = LogLevel.Debug, Message = "Executing function with concurrency control. Active: {ActiveRequests}, Max: {MaxConcurrent}")]
        public static partial void ExecutingWithConcurrencyControl(this ILogger logger, long activeRequests, int maxConcurrent);

        [LoggerMessage(EventId = 15001, Level = LogLevel.Debug, Message = "Function executed successfully in {ElapsedMs}ms")]
        public static partial void FunctionExecutedSuccessfully(this ILogger logger, long elapsedMs);

        [LoggerMessage(EventId = 15002, Level = LogLevel.Error, Message = "Function execution failed")]
        public static partial void FunctionExecutionFailed(this ILogger logger, Exception exception);

        [LoggerMessage(EventId = 15003, Level = LogLevel.Information, Message = "Executing {FunctionCount} functions concurrently")]
        public static partial void ExecutingFunctionsConcurrently(this ILogger logger, int functionCount);

        [LoggerMessage(EventId = 15004, Level = LogLevel.Information, Message = "Successfully executed {FunctionCount} concurrent functions")]
        public static partial void SuccessfullyExecutedConcurrentFunctions(this ILogger logger, int functionCount);

        [LoggerMessage(EventId = 15005, Level = LogLevel.Error, Message = "Error executing multiple concurrent functions")]
        public static partial void ErrorExecutingConcurrentFunctions(this ILogger logger, Exception exception);

        [LoggerMessage(EventId = 15006, Level = LogLevel.Debug, Message = "Retry attempt {Attempt}/{MaxRetries} after {DelayMs}ms")]
        public static partial void RetryAttempt(this ILogger logger, int attempt, int maxRetries, double delayMs);

        [LoggerMessage(EventId = 15007, Level = LogLevel.Warning, Message = "Function execution cancelled during retry attempt {Attempt}")]
        public static partial void FunctionExecutionCancelled(this ILogger logger, int attempt);

        [LoggerMessage(EventId = 15008, Level = LogLevel.Error, Message = "Function execution failed after {MaxRetries} retries")]
        public static partial void FunctionExecutionFailedAfterRetries(this ILogger logger, Exception exception, int maxRetries);

        [LoggerMessage(EventId = 15009, Level = LogLevel.Warning, Message = "Retriable exception on attempt {Attempt}/{MaxRetries}: {ErrorMessage}")]
        public static partial void RetriableException(this ILogger logger, Exception exception, int attempt, int maxRetries, string errorMessage);

        [LoggerMessage(EventId = 15010, Level = LogLevel.Error, Message = "Non-retriable exception on attempt {Attempt}, aborting retries")]
        public static partial void NonRetriableException(this ILogger logger, Exception exception, int attempt);

        [LoggerMessage(EventId = 15011, Level = LogLevel.Debug, Message = "Created timeout token with {TimeoutSeconds}s timeout")]
        public static partial void CreatedTimeoutToken(this ILogger logger, int timeoutSeconds);

        [LoggerMessage(EventId = 15012, Level = LogLevel.Debug, Message = "Disposing RequestProcessingService")]
        public static partial void DisposingService(this ILogger logger);
    }
}
