using Microsoft.Extensions.Logging;
using OpenReferralApi.Core.Helpers;

namespace OpenReferralApi.Core.Logging;

internal static class MemoryCheckpointLog
{
    public static void UnifiedMemoryCheckpoint(this ILogger logger, MemoryCheckpointLogPayload payload)
    {
        logger.LogInformation(
            "Memory checkpoint {Service}/{Stage}. CorrelationId: {CorrelationId}, BaseUrl: {BaseUrl}, Profile: {Profile}, GroupName: {GroupName}, ManagedHeapBytes: {ManagedHeapBytes}, ManagedHeapDeltaBytes: {ManagedHeapDeltaBytes}, ProcessWorkingSetBytes: {ProcessWorkingSetBytes}, ProcessWorkingSetDeltaBytes: {ProcessWorkingSetDeltaBytes}, GcHeapSizeBytes: {GcHeapSizeBytes}, GcHeapSizeDeltaBytes: {GcHeapSizeDeltaBytes}, GcFragmentedBytes: {GcFragmentedBytes}, GcFragmentedDeltaBytes: {GcFragmentedDeltaBytes}, GcTotalCommittedBytes: {GcTotalCommittedBytes}, GcTotalCommittedDeltaBytes: {GcTotalCommittedDeltaBytes}, GcMemoryLoadBytes: {GcMemoryLoadBytes}, GcMemoryLoadDeltaBytes: {GcMemoryLoadDeltaBytes}, Gen0CollectionsDelta: {Gen0CollectionsDelta}, Gen1CollectionsDelta: {Gen1CollectionsDelta}, Gen2CollectionsDelta: {Gen2CollectionsDelta}, ElapsedMs: {ElapsedMs}, AccumulatedEndpointResults: {AccumulatedEndpointResults}, FeedCacheEntries: {FeedCacheEntries}, ProfileCacheEntries: {ProfileCacheEntries}, ExpiredCacheEntries: {ExpiredCacheEntries}, FeedJsonChars: {FeedJsonChars}, ProfileJsonChars: {ProfileJsonChars}, CompiledSchemaCacheEntryCount: {CompiledSchemaCacheEntryCount}, CompiledSchemaCacheTotalKeyChars: {CompiledSchemaCacheTotalKeyChars}, ParsedJsonDocumentsInFlight: {ParsedJsonDocumentsInFlight}, RetainedResponseBodies: {RetainedResponseBodies}, RetainedResponseBodyChars: {RetainedResponseBodyChars}, ExtractedIdRoots: {ExtractedIdRoots}, ExtractedIdValues: {ExtractedIdValues}, ValidationSchemaCacheEntries: {ValidationSchemaCacheEntries}",
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
