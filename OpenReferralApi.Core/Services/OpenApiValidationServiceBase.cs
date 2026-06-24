using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json.Nodes;
using OpenReferralApi.Core.Helpers;

namespace OpenReferralApi.Core.Services;

public abstract class OpenApiValidationServiceBase
{
    private const string ValidationMetricsMeterName = "OpenReferralApi.Core.OpenApiValidationService";

    protected static readonly ConcurrentDictionary<string, CachedResolvedSpec> FeedResolvedSpecCache = new(StringComparer.OrdinalIgnoreCase);
    protected static readonly ConcurrentDictionary<string, CachedResolvedSpec> ProfileResolvedSpecCache = new(StringComparer.OrdinalIgnoreCase);

    protected static readonly Meter CacheMetricsMeter = new(ValidationMetricsMeterName, "1.0.0");

    protected static readonly Counter<long> ResolvedOpenApiCacheHitsCounter = CacheMetricsMeter.CreateCounter<long>(
        "openreferral.openapi.cache.hits",
        description: "Number of resolved OpenAPI cache hits by scope (feed/profile)");

    protected static readonly Counter<long> ResolvedOpenApiCacheMissesCounter = CacheMetricsMeter.CreateCounter<long>(
        "openreferral.openapi.cache.misses",
        description: "Number of resolved OpenAPI cache misses by scope (feed/profile)");

    protected static readonly ObservableGauge<int> FeedResolvedOpenApiCacheEntriesGauge = CacheMetricsMeter.CreateObservableGauge(
        "openreferral.openapi.cache.entries.feed",
        () => FeedResolvedSpecCache.Count,
        description: "Number of cached resolved feed OpenAPI specifications");

    protected static readonly ObservableGauge<int> ProfileResolvedOpenApiCacheEntriesGauge = CacheMetricsMeter.CreateObservableGauge(
        "openreferral.openapi.cache.entries.profile",
        () => ProfileResolvedSpecCache.Count,
        description: "Number of cached resolved profile OpenAPI specifications");

    protected static readonly ObservableGauge<int> ResolvedOpenApiExpiredEntriesGauge = CacheMetricsMeter.CreateObservableGauge(
        "openreferral.openapi.cache.entries.expired",
        CountExpiredCacheEntries,
        description: "Number of expired cached resolved OpenAPI specifications (feed + profile)");

    protected static readonly Histogram<long> ValidationManagedHeapBytesHistogram = CacheMetricsMeter.CreateHistogram<long>(
        "openreferral.openapi.validation.memory.managed_heap_bytes",
        unit: "By",
        description: "Managed heap size observed at validation memory checkpoints");

    protected static readonly Histogram<long> ValidationManagedHeapDeltaBytesHistogram = CacheMetricsMeter.CreateHistogram<long>(
        "openreferral.openapi.validation.memory.managed_heap_delta_bytes",
        unit: "By",
        description: "Managed heap delta between validation memory checkpoints");

    protected static readonly Histogram<long> ValidationWorkingSetBytesHistogram = CacheMetricsMeter.CreateHistogram<long>(
        "openreferral.openapi.validation.memory.working_set_bytes",
        unit: "By",
        description: "Process working set observed at validation memory checkpoints");

    protected static readonly Histogram<long> ValidationWorkingSetDeltaBytesHistogram = CacheMetricsMeter.CreateHistogram<long>(
        "openreferral.openapi.validation.memory.working_set_delta_bytes",
        unit: "By",
        description: "Process working set delta between validation memory checkpoints");

    protected sealed record CachedResolvedSpec(string ResolvedSpecJson, JsonObject ResolvedSpecDocument, DateTime ExpiresAtUtc);

    protected static int CountExpiredCacheEntries()
    {
        var now = DateTime.UtcNow;
        var expiredFeedEntries = FeedResolvedSpecCache.Values.Count(entry => entry.ExpiresAtUtc <= now);
        var expiredProfileEntries = ProfileResolvedSpecCache.Values.Count(entry => entry.ExpiresAtUtc <= now);
        return expiredFeedEntries + expiredProfileEntries;
    }

    protected static (int FeedEntries, int ProfileEntries, int ExpiredEntries, long FeedJsonChars, long ProfileJsonChars) GetResolvedOpenApiCacheState()
    {
        var now = DateTime.UtcNow;
        var expiredEntries = FeedResolvedSpecCache.Values.Count(entry => entry.ExpiresAtUtc <= now)
            + ProfileResolvedSpecCache.Values.Count(entry => entry.ExpiresAtUtc <= now);

        return (
            FeedResolvedSpecCache.Count,
            ProfileResolvedSpecCache.Count,
            expiredEntries,
            FeedResolvedSpecCache.Values.Sum(entry => (long)entry.ResolvedSpecJson.Length),
            ProfileResolvedSpecCache.Values.Sum(entry => (long)entry.ResolvedSpecJson.Length));
    }

    protected static void PurgeExpiredCacheEntries()
    {
        var now = DateTime.UtcNow;
        foreach (var key in FeedResolvedSpecCache.Keys.ToList())
        {
            if (FeedResolvedSpecCache.TryGetValue(key, out var entry) && entry.ExpiresAtUtc <= now)
            {
                _ = FeedResolvedSpecCache.TryRemove(key, out _);
            }
        }

        foreach (var key in ProfileResolvedSpecCache.Keys.ToList())
        {
            if (ProfileResolvedSpecCache.TryGetValue(key, out var entry) && entry.ExpiresAtUtc <= now)
            {
                _ = ProfileResolvedSpecCache.TryRemove(key, out _);
            }
        }
    }

    protected static ConcurrentDictionary<string, CachedResolvedSpec> ResolveCacheByScope(string cacheScope)
    {
        return cacheScope switch
        {
            "feed" => FeedResolvedSpecCache,
            "profile" => ProfileResolvedSpecCache,
            _ => throw new ArgumentOutOfRangeException(nameof(cacheScope), cacheScope, "Cache scope must be either 'feed' or 'profile'.")
        };
    }

    protected static bool IsLikelyOpenApiDocument(JsonObject candidate)
    {
        return candidate.ContainsKey("openapi")
            || candidate.ContainsKey("swagger")
            || candidate.ContainsKey("paths");
    }

    protected static string ResolveMemoryCheckpointCorrelationId()
    {
        return Activity.Current?.TraceId.ToString()
            ?? Activity.Current?.Id
            ?? "n/a";
    }

    private protected static MemoryCheckpointLogPayload CreateMemoryCheckpointPayload(
        string service,
        string stage,
        string sanitizedBaseUrl,
        MemoryCheckpointSnapshot snapshot,
        string? profile = null,
        string? groupName = null,
        int? accumulatedEndpointResults = null)
    {
        return new MemoryCheckpointLogPayload
        {
            Service = service,
            Stage = stage,
            CorrelationId = ResolveMemoryCheckpointCorrelationId(),
            BaseUrl = sanitizedBaseUrl,
            Profile = profile,
            GroupName = groupName,
            ManagedHeapBytes = snapshot.ManagedHeapBytes,
            ManagedHeapDeltaBytes = snapshot.ManagedHeapDeltaBytes,
            ProcessWorkingSetBytes = snapshot.ProcessWorkingSetBytes,
            ProcessWorkingSetDeltaBytes = snapshot.ProcessWorkingSetDeltaBytes,
            GcHeapSizeBytes = snapshot.GcHeapSizeBytes,
            GcHeapSizeDeltaBytes = snapshot.GcHeapSizeDeltaBytes,
            GcFragmentedBytes = snapshot.GcFragmentedBytes,
            GcFragmentedDeltaBytes = snapshot.GcFragmentedDeltaBytes,
            GcTotalCommittedBytes = snapshot.GcTotalCommittedBytes,
            GcTotalCommittedDeltaBytes = snapshot.GcTotalCommittedDeltaBytes,
            GcMemoryLoadBytes = snapshot.GcMemoryLoadBytes,
            GcMemoryLoadDeltaBytes = snapshot.GcMemoryLoadDeltaBytes,
            Gen0CollectionsDelta = snapshot.Gen0CollectionsDelta,
            Gen1CollectionsDelta = snapshot.Gen1CollectionsDelta,
            Gen2CollectionsDelta = snapshot.Gen2CollectionsDelta,
            ElapsedMilliseconds = snapshot.ElapsedMilliseconds,
            AccumulatedEndpointResults = accumulatedEndpointResults
        };
    }
}
