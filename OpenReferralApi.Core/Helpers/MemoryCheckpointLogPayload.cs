namespace OpenReferralApi.Core.Helpers;

internal sealed record MemoryCheckpointLogPayload
{
    public required string Service { get; init; }
    public required string Stage { get; init; }
    public required string CorrelationId { get; init; }
    public required string BaseUrl { get; init; }

    public string? Profile { get; init; }
    public string? GroupName { get; init; }

    public long ManagedHeapBytes { get; init; }
    public long ManagedHeapDeltaBytes { get; init; }
    public long ProcessWorkingSetBytes { get; init; }
    public long ProcessWorkingSetDeltaBytes { get; init; }

    public long GcHeapSizeBytes { get; init; }
    public long GcHeapSizeDeltaBytes { get; init; }
    public long GcFragmentedBytes { get; init; }
    public long GcFragmentedDeltaBytes { get; init; }
    public long GcTotalCommittedBytes { get; init; }
    public long GcTotalCommittedDeltaBytes { get; init; }
    public long GcMemoryLoadBytes { get; init; }
    public long GcMemoryLoadDeltaBytes { get; init; }
    public int Gen0CollectionsDelta { get; init; }
    public int Gen1CollectionsDelta { get; init; }
    public int Gen2CollectionsDelta { get; init; }

    public double ElapsedMilliseconds { get; init; }

    public int? AccumulatedEndpointResults { get; init; }

    public int? FeedCacheEntries { get; init; }
    public int? ProfileCacheEntries { get; init; }
    public int? ExpiredCacheEntries { get; init; }
    public long? FeedJsonChars { get; init; }
    public long? ProfileJsonChars { get; init; }

    public int? CompiledSchemaCacheEntryCount { get; init; }
    public long? CompiledSchemaCacheTotalKeyChars { get; init; }

    public int? ParsedJsonDocumentsInFlight { get; init; }
    public int? RetainedResponseBodies { get; init; }
    public long? RetainedResponseBodyChars { get; init; }
    public int? ExtractedIdRoots { get; init; }
    public int? ExtractedIdValues { get; init; }
    public int? ValidationSchemaCacheEntries { get; init; }
}
