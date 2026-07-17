using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenReferralApi.Core.Helpers;
using OpenReferralApi.Core.Logging;
using OpenReferralApi.Core.Services;

namespace OpenReferralApi.Services
{
    internal interface ISchemaWarmupExecutor
    {
        Task WarmupAsync(
            SpecificationOptions options,
            CacheOptions cacheOptions,
            ISchemaWarmupStatusTracker statusTracker,
            IServiceProvider serviceProvider,
            ILogger logger,
            CancellationToken cancellationToken);
    }

    internal sealed class SchemaWarmupExecutor : ISchemaWarmupExecutor
    {
        private const string SchemaWarmupMetricsMeterName = "OpenReferralApi.SchemaWarmupExecutor";
        private static readonly Meter SchemaWarmupMetricsMeter = new(SchemaWarmupMetricsMeterName, "1.0.0");
        private static readonly Histogram<long> WarmupManagedHeapBytesHistogram = SchemaWarmupMetricsMeter.CreateHistogram<long>(
            "openreferral.schema_warmup.memory.managed_heap_bytes",
            unit: "By",
            description: "Managed heap size observed at schema warmup memory checkpoints");
        private static readonly Histogram<long> WarmupManagedHeapDeltaBytesHistogram = SchemaWarmupMetricsMeter.CreateHistogram<long>(
            "openreferral.schema_warmup.memory.managed_heap_delta_bytes",
            unit: "By",
            description: "Managed heap delta between schema warmup memory checkpoints");
        private static readonly Histogram<long> WarmupWorkingSetBytesHistogram = SchemaWarmupMetricsMeter.CreateHistogram<long>(
            "openreferral.schema_warmup.memory.working_set_bytes",
            unit: "By",
            description: "Process working set observed at schema warmup memory checkpoints");
        private static readonly Histogram<long> WarmupWorkingSetDeltaBytesHistogram = SchemaWarmupMetricsMeter.CreateHistogram<long>(
            "openreferral.schema_warmup.memory.working_set_delta_bytes",
            unit: "By",
            description: "Process working set delta between schema warmup memory checkpoints");
        private static readonly Action<ILogger, Exception?> LogWarmupDisabled =
            LoggerMessage.Define(LogLevel.Information, new EventId(1, nameof(LogWarmupDisabled)), "Warmup disabled");

        private static readonly Action<ILogger, Exception?> LogWarmupCacheDisabled =
            LoggerMessage.Define(LogLevel.Information, new EventId(2, nameof(LogWarmupCacheDisabled)), "Warmup skipped: cache disabled");

        private static readonly Action<ILogger, Exception?> LogWarmupNoUrls =
            LoggerMessage.Define(LogLevel.Information, new EventId(3, nameof(LogWarmupNoUrls)), "Warmup: no URLs configured");

        private static readonly Action<ILogger, int, Exception?> LogWarmupCompleted =
            LoggerMessage.Define<int>(LogLevel.Information, new EventId(4, nameof(LogWarmupCompleted)), "Warmup completed for {UrlCount} URLs");

        public async Task WarmupAsync(
            SpecificationOptions options,
            CacheOptions cacheOptions,
            ISchemaWarmupStatusTracker statusTracker,
            IServiceProvider serviceProvider,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var urls = (options.Urls ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase))
                .Values
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Select(url => url.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!options.WarmupEnabled)
            {
                statusTracker.MarkSkipped("disabled");
                LogWarmupDisabled(logger, null);
                return;
            }

            if (!cacheOptions.Enabled)
            {
                statusTracker.MarkSkipped("cache-disabled");
                LogWarmupCacheDisabled(logger, null);
                return;
            }

            if (urls.Count == 0)
            {
                statusTracker.MarkSkipped("no-urls");
                LogWarmupNoUrls(logger, null);
                return;
            }

            statusTracker.MarkStarted(urls.Count);
            try
            {
                var stopwatch = Stopwatch.StartNew();
                var lastManagedHeapBytes = GC.GetTotalMemory(forceFullCollection: false);
                var lastWorkingSetBytes = Environment.WorkingSet;
                var cachedSchemaCount = 0;

                if (options.WarmupStartupDelaySeconds > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(options.WarmupStartupDelaySeconds), cancellationToken).ConfigureAwait(false);
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    statusTracker.MarkCompleted(true);
                    return;
                }

                using var scope = serviceProvider.CreateScope();
                var resolver = scope.ServiceProvider.GetRequiredService<ISchemaResolverService>();

                logger.WarmupMemoryBeforeCaching(
                    urls.Count,
                    lastManagedHeapBytes,
                    lastWorkingSetBytes,
                    stopwatch.Elapsed.TotalMilliseconds);
                RecordWarmupMemoryMetrics("before-caching", "pre", lastManagedHeapBytes, 0, lastWorkingSetBytes, 0);

                foreach (var url in urls)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        statusTracker.MarkCompleted(true);
                        return;
                    }

                    var warmupSchemaRef = $$"""
                    {
                      "$ref": "{{url}}"
                    }
                    """;

                    try
                    {
                        await resolver.ResolveAsync(warmupSchemaRef, url, auth: null).ConfigureAwait(false);
                        statusTracker.MarkSuccess();
                        cachedSchemaCount++;

                        var managedHeapBytes = GC.GetTotalMemory(forceFullCollection: false);
                        var processWorkingSetBytes = Environment.WorkingSet;
                        var sanitizedUrl = TextSanitizer.SanitizeUrlForLogging(url);
                        logger.WarmupMemoryAfterCaching(
                            sanitizedUrl,
                            cachedSchemaCount,
                            urls.Count,
                            managedHeapBytes,
                            managedHeapBytes - lastManagedHeapBytes,
                            processWorkingSetBytes,
                            processWorkingSetBytes - lastWorkingSetBytes,
                            stopwatch.Elapsed.TotalMilliseconds);

                        RecordWarmupMemoryMetrics(
                            "after-attempt",
                            "success",
                            managedHeapBytes,
                            managedHeapBytes - lastManagedHeapBytes,
                            processWorkingSetBytes,
                            processWorkingSetBytes - lastWorkingSetBytes);

                        lastManagedHeapBytes = managedHeapBytes;
                        lastWorkingSetBytes = processWorkingSetBytes;
                    }
                    catch (InvalidOperationException)
                    {
                        statusTracker.MarkFailure(url);
                        RecordWarmupFailureMemoryMetrics(logger, url, cachedSchemaCount, urls.Count, stopwatch.Elapsed.TotalMilliseconds, ref lastManagedHeapBytes, ref lastWorkingSetBytes);
                    }
                    catch (HttpRequestException)
                    {
                        statusTracker.MarkFailure(url);
                        RecordWarmupFailureMemoryMetrics(logger, url, cachedSchemaCount, urls.Count, stopwatch.Elapsed.TotalMilliseconds, ref lastManagedHeapBytes, ref lastWorkingSetBytes);
                    }
                    catch (UriFormatException)
                    {
                        statusTracker.MarkFailure(url);
                        RecordWarmupFailureMemoryMetrics(logger, url, cachedSchemaCount, urls.Count, stopwatch.Elapsed.TotalMilliseconds, ref lastManagedHeapBytes, ref lastWorkingSetBytes);
                    }
                    catch (TaskCanceledException)
                    {
                        statusTracker.MarkFailure(url);
                        RecordWarmupFailureMemoryMetrics(logger, url, cachedSchemaCount, urls.Count, stopwatch.Elapsed.TotalMilliseconds, ref lastManagedHeapBytes, ref lastWorkingSetBytes);
                    }
                    catch (ArgumentException)
                    {
                        statusTracker.MarkFailure(url);
                        RecordWarmupFailureMemoryMetrics(logger, url, cachedSchemaCount, urls.Count, stopwatch.Elapsed.TotalMilliseconds, ref lastManagedHeapBytes, ref lastWorkingSetBytes);
                    }
                }

                statusTracker.MarkCompleted(false);
                LogWarmupCompleted(logger, urls.Count, null);
            }
            catch (OperationCanceledException)
            {
                statusTracker.MarkCompleted(true);
            }
        }

        private static void RecordWarmupMemoryMetrics(string stage, string outcome, long managedHeapBytes, long managedHeapDeltaBytes, long workingSetBytes, long workingSetDeltaBytes)
        {
            var tags = new TagList
            {
                { "stage", stage },
                { "outcome", outcome }
            };

            WarmupManagedHeapBytesHistogram.Record(managedHeapBytes, tags);
            WarmupManagedHeapDeltaBytesHistogram.Record(managedHeapDeltaBytes, tags);
            WarmupWorkingSetBytesHistogram.Record(workingSetBytes, tags);
            WarmupWorkingSetDeltaBytesHistogram.Record(workingSetDeltaBytes, tags);
        }

        private static void RecordWarmupFailureMemoryMetrics(ILogger logger, string schemaUrl, int cachedSchemaCount, int totalSchemaCount, double elapsedMs, ref long lastManagedHeapBytes, ref long lastWorkingSetBytes)
        {
            var managedHeapBytes = GC.GetTotalMemory(forceFullCollection: false);
            var workingSetBytes = Environment.WorkingSet;

            logger.WarmupMemoryAfterFailure(
                TextSanitizer.SanitizeUrlForLogging(schemaUrl),
                cachedSchemaCount,
                totalSchemaCount,
                managedHeapBytes,
                managedHeapBytes - lastManagedHeapBytes,
                workingSetBytes,
                workingSetBytes - lastWorkingSetBytes,
                elapsedMs);

            RecordWarmupMemoryMetrics(
                "after-attempt",
                "failure",
                managedHeapBytes,
                managedHeapBytes - lastManagedHeapBytes,
                workingSetBytes,
                workingSetBytes - lastWorkingSetBytes);

            lastManagedHeapBytes = managedHeapBytes;
            lastWorkingSetBytes = workingSetBytes;
        }
    }
}
