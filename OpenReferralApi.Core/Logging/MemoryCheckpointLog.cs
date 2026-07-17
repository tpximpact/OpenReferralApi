using Microsoft.Extensions.Logging;
using OpenReferralApi.Core.Helpers;

namespace OpenReferralApi.Core.Logging;

internal static partial class MemoryCheckpointLog
{
    [LoggerMessage(
        EventId = 12000,
        Level = LogLevel.Information,
        Message = "Memory checkpoint {Service}/{Stage}. CorrelationId: {CorrelationId}, BaseUrl: {BaseUrl}, Profile: {Profile}, GroupName: {GroupName}, ManagedHeapBytes: {ManagedHeapBytes}, ManagedHeapDeltaBytes: {ManagedHeapDeltaBytes}, ProcessWorkingSetBytes: {ProcessWorkingSetBytes}, ProcessWorkingSetDeltaBytes: {ProcessWorkingSetDeltaBytes}, GcHeapSizeBytes: {GcHeapSizeBytes}, GcHeapSizeDeltaBytes: {GcHeapSizeDeltaBytes}, GcFragmentedBytes: {GcFragmentedBytes}, GcFragmentedDeltaBytes: {GcFragmentedDeltaBytes}, GcTotalCommittedBytes: {GcTotalCommittedBytes}, GcTotalCommittedDeltaBytes: {GcTotalCommittedDeltaBytes}, GcMemoryLoadBytes: {GcMemoryLoadBytes}, GcMemoryLoadDeltaBytes: {GcMemoryLoadDeltaBytes}, Gen0CollectionsDelta: {Gen0CollectionsDelta}, Gen1CollectionsDelta: {Gen1CollectionsDelta}, Gen2CollectionsDelta: {Gen2CollectionsDelta}, ElapsedMs: {ElapsedMs}, AccumulatedEndpointResults: {AccumulatedEndpointResults}, FeedCacheEntries: {FeedCacheEntries}, ProfileCacheEntries: {ProfileCacheEntries}, ExpiredCacheEntries: {ExpiredCacheEntries}, FeedJsonChars: {FeedJsonChars}, ProfileJsonChars: {ProfileJsonChars}, CompiledSchemaCacheEntryCount: {CompiledSchemaCacheEntryCount}, CompiledSchemaCacheTotalKeyChars: {CompiledSchemaCacheTotalKeyChars}, ParsedJsonDocumentsInFlight: {ParsedJsonDocumentsInFlight}, RetainedResponseBodies: {RetainedResponseBodies}, RetainedResponseBodyChars: {RetainedResponseBodyChars}, ExtractedIdRoots: {ExtractedIdRoots}, ExtractedIdValues: {ExtractedIdValues}, ValidationSchemaCacheEntries: {ValidationSchemaCacheEntries}")]
    private static partial void LogUnifiedMemoryCheckpoint(
        this ILogger logger,
        string Service,
        string Stage,
        string CorrelationId,
        string BaseUrl,
        string? Profile,
        string? GroupName,
        long ManagedHeapBytes,
        long ManagedHeapDeltaBytes,
        long ProcessWorkingSetBytes,
        long ProcessWorkingSetDeltaBytes,
        long GcHeapSizeBytes,
        long GcHeapSizeDeltaBytes,
        long GcFragmentedBytes,
        long GcFragmentedDeltaBytes,
        long GcTotalCommittedBytes,
        long GcTotalCommittedDeltaBytes,
        long GcMemoryLoadBytes,
        long GcMemoryLoadDeltaBytes,
        int Gen0CollectionsDelta,
        int Gen1CollectionsDelta,
        int Gen2CollectionsDelta,
        double ElapsedMs,
        int? AccumulatedEndpointResults,
        int? FeedCacheEntries,
        int? ProfileCacheEntries,
        int? ExpiredCacheEntries,
        long? FeedJsonChars,
        long? ProfileJsonChars,
        int? CompiledSchemaCacheEntryCount,
        long? CompiledSchemaCacheTotalKeyChars,
        int? ParsedJsonDocumentsInFlight,
        int? RetainedResponseBodies,
        long? RetainedResponseBodyChars,
        int? ExtractedIdRoots,
        int? ExtractedIdValues,
        int? ValidationSchemaCacheEntries);

    public static void UnifiedMemoryCheckpoint(this ILogger logger, MemoryCheckpointLogPayload payload)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            LogUnifiedMemoryCheckpoint(
                logger,
                payload.Service,
                payload.Stage,
                payload.CorrelationId,
                payload.BaseUrl,
                payload.Profile,
                payload.GroupName,
                payload.ManagedHeapBytes,
                payload.ManagedHeapDeltaBytes,
                payload.ProcessWorkingSetBytes,
                payload.ProcessWorkingSetDeltaBytes,
                payload.GcHeapSizeBytes,
                payload.GcHeapSizeDeltaBytes,
                payload.GcFragmentedBytes,
                payload.GcFragmentedDeltaBytes,
                payload.GcTotalCommittedBytes,
                payload.GcTotalCommittedDeltaBytes,
                payload.GcMemoryLoadBytes,
                payload.GcMemoryLoadDeltaBytes,
                payload.Gen0CollectionsDelta,
                payload.Gen1CollectionsDelta,
                payload.Gen2CollectionsDelta,
                payload.ElapsedMilliseconds,
                payload.AccumulatedEndpointResults,
                payload.FeedCacheEntries,
                payload.ProfileCacheEntries,
                payload.ExpiredCacheEntries,
                payload.FeedJsonChars,
                payload.ProfileJsonChars,
                payload.CompiledSchemaCacheEntryCount,
                payload.CompiledSchemaCacheTotalKeyChars,
                payload.ParsedJsonDocumentsInFlight,
                payload.RetainedResponseBodies,
                payload.RetainedResponseBodyChars,
                payload.ExtractedIdRoots,
                payload.ExtractedIdValues,
                payload.ValidationSchemaCacheEntries);
        }
    }
}
