using Microsoft.Extensions.Logging;

namespace OpenReferralApi.Core.Logging
{
    public static partial class SchemaWarmupLog
    {
        [LoggerMessage(EventId = 3000, Level = LogLevel.Information, Message = "Schema warmup is disabled.")]
        public static partial void WarmupDisabled(this ILogger logger);

        [LoggerMessage(EventId = 3001, Level = LogLevel.Information, Message = "Schema warmup is skipped because cache is disabled.")]
        public static partial void WarmupSkippedCacheDisabled(this ILogger logger);

        [LoggerMessage(EventId = 3002, Level = LogLevel.Information, Message = "Schema warmup is enabled but no URLs are configured.")]
        public static partial void WarmupNoUrlsConfigured(this ILogger logger);

        [LoggerMessage(EventId = 3003, Level = LogLevel.Information, Message = "Schema warmup starting in {DelaySeconds}s.")]
        public static partial void WarmupStartingIn(this ILogger logger, int delaySeconds);

        [LoggerMessage(EventId = 3004, Level = LogLevel.Information, Message = "Starting schema warmup for {Count} URL(s).")]
        public static partial void WarmupStartingForUrls(this ILogger logger, int count);

        [LoggerMessage(EventId = 3005, Level = LogLevel.Information, Message = "Schema warmup succeeded: {SchemaUrl}")]
        public static partial void WarmupSucceeded(this ILogger logger, string schemaUrl);

        [LoggerMessage(EventId = 3006, Level = LogLevel.Warning, Message = "Schema warmup failed: {SchemaUrl}")]
        public static partial void WarmupFailed(this ILogger logger, Exception exception, string schemaUrl);

        [LoggerMessage(EventId = 3007, Level = LogLevel.Information, Message = "Schema warmup completed.")]
        public static partial void WarmupCompleted(this ILogger logger);

        [LoggerMessage(EventId = 3008, Level = LogLevel.Information, Message = "Schema warmup memory checkpoint before caching begins. UrlCount: {UrlCount}, ManagedHeapBytes: {ManagedHeapBytes}, ProcessWorkingSetBytes: {ProcessWorkingSetBytes}, ElapsedMs: {ElapsedMs}")]
        public static partial void WarmupMemoryBeforeCaching(this ILogger logger, int urlCount, long managedHeapBytes, long processWorkingSetBytes, double elapsedMs);

        [LoggerMessage(EventId = 3009, Level = LogLevel.Information, Message = "Schema warmup memory checkpoint after attempt outcome success for schema {SchemaUrl}. CachedSchemaCount: {CachedSchemaCount}, TotalSchemaCount: {TotalSchemaCount}, ManagedHeapBytes: {ManagedHeapBytes}, ManagedHeapDeltaBytes: {ManagedHeapDeltaBytes}, ProcessWorkingSetBytes: {ProcessWorkingSetBytes}, ProcessWorkingSetDeltaBytes: {ProcessWorkingSetDeltaBytes}, ElapsedMs: {ElapsedMs}")]
        public static partial void WarmupMemoryAfterCaching(this ILogger logger, string schemaUrl, int cachedSchemaCount, int totalSchemaCount, long managedHeapBytes, long managedHeapDeltaBytes, long processWorkingSetBytes, long processWorkingSetDeltaBytes, double elapsedMs);

        [LoggerMessage(EventId = 3010, Level = LogLevel.Warning, Message = "Schema warmup memory checkpoint after attempt outcome failure for schema {SchemaUrl}. CachedSchemaCount: {CachedSchemaCount}, TotalSchemaCount: {TotalSchemaCount}, ManagedHeapBytes: {ManagedHeapBytes}, ManagedHeapDeltaBytes: {ManagedHeapDeltaBytes}, ProcessWorkingSetBytes: {ProcessWorkingSetBytes}, ProcessWorkingSetDeltaBytes: {ProcessWorkingSetDeltaBytes}, ElapsedMs: {ElapsedMs}")]
        public static partial void WarmupMemoryAfterFailure(this ILogger logger, string schemaUrl, int cachedSchemaCount, int totalSchemaCount, long managedHeapBytes, long managedHeapDeltaBytes, long processWorkingSetBytes, long processWorkingSetDeltaBytes, double elapsedMs);
    }
}
