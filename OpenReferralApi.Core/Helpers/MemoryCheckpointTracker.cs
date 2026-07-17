using System.Diagnostics;

namespace OpenReferralApi.Core.Helpers;

internal sealed class MemoryCheckpointTracker
{
    private readonly Stopwatch _stopwatch;
    private long _lastManagedHeapBytes;
    private long _lastWorkingSetBytes;
    private long _lastGcHeapSizeBytes;
    private long _lastGcFragmentedBytes;
    private long _lastGcTotalCommittedBytes;
    private long _lastGcMemoryLoadBytes;
    private int _lastGen0CollectionCount;
    private int _lastGen1CollectionCount;
    private int _lastGen2CollectionCount;

    public MemoryCheckpointTracker(Stopwatch stopwatch)
    {
        _stopwatch = stopwatch;

        _lastManagedHeapBytes = GC.GetTotalMemory(forceFullCollection: false);
        _lastWorkingSetBytes = Environment.WorkingSet;

        var initialGcMemoryInfo = GC.GetGCMemoryInfo();
        _lastGcHeapSizeBytes = initialGcMemoryInfo.HeapSizeBytes;
        _lastGcFragmentedBytes = initialGcMemoryInfo.FragmentedBytes;
        _lastGcTotalCommittedBytes = initialGcMemoryInfo.TotalCommittedBytes;
        _lastGcMemoryLoadBytes = initialGcMemoryInfo.MemoryLoadBytes;
        _lastGen0CollectionCount = GC.CollectionCount(0);
        _lastGen1CollectionCount = GC.CollectionCount(1);
        _lastGen2CollectionCount = GC.CollectionCount(2);
    }

    public MemoryCheckpointSnapshot Capture()
    {
        var managedHeapBytes = GC.GetTotalMemory(forceFullCollection: false);
        var managedHeapDeltaBytes = managedHeapBytes - _lastManagedHeapBytes;
        var processWorkingSetBytes = Environment.WorkingSet;
        var processWorkingSetDeltaBytes = processWorkingSetBytes - _lastWorkingSetBytes;

        var gcMemoryInfo = GC.GetGCMemoryInfo();
        var gcHeapSizeBytes = gcMemoryInfo.HeapSizeBytes;
        var gcHeapSizeDeltaBytes = gcHeapSizeBytes - _lastGcHeapSizeBytes;
        var gcFragmentedBytes = gcMemoryInfo.FragmentedBytes;
        var gcFragmentedDeltaBytes = gcFragmentedBytes - _lastGcFragmentedBytes;
        var gcTotalCommittedBytes = gcMemoryInfo.TotalCommittedBytes;
        var gcTotalCommittedDeltaBytes = gcTotalCommittedBytes - _lastGcTotalCommittedBytes;
        var gcMemoryLoadBytes = gcMemoryInfo.MemoryLoadBytes;
        var gcMemoryLoadDeltaBytes = gcMemoryLoadBytes - _lastGcMemoryLoadBytes;
        var gen0CollectionCount = GC.CollectionCount(0);
        var gen1CollectionCount = GC.CollectionCount(1);
        var gen2CollectionCount = GC.CollectionCount(2);
        var gen0CollectionsDelta = gen0CollectionCount - _lastGen0CollectionCount;
        var gen1CollectionsDelta = gen1CollectionCount - _lastGen1CollectionCount;
        var gen2CollectionsDelta = gen2CollectionCount - _lastGen2CollectionCount;

        _lastManagedHeapBytes = managedHeapBytes;
        _lastWorkingSetBytes = processWorkingSetBytes;
        _lastGcHeapSizeBytes = gcHeapSizeBytes;
        _lastGcFragmentedBytes = gcFragmentedBytes;
        _lastGcTotalCommittedBytes = gcTotalCommittedBytes;
        _lastGcMemoryLoadBytes = gcMemoryLoadBytes;
        _lastGen0CollectionCount = gen0CollectionCount;
        _lastGen1CollectionCount = gen1CollectionCount;
        _lastGen2CollectionCount = gen2CollectionCount;

        return new MemoryCheckpointSnapshot(
            managedHeapBytes,
            managedHeapDeltaBytes,
            processWorkingSetBytes,
            processWorkingSetDeltaBytes,
            gcHeapSizeBytes,
            gcHeapSizeDeltaBytes,
            gcFragmentedBytes,
            gcFragmentedDeltaBytes,
            gcTotalCommittedBytes,
            gcTotalCommittedDeltaBytes,
            gcMemoryLoadBytes,
            gcMemoryLoadDeltaBytes,
            gen0CollectionsDelta,
            gen1CollectionsDelta,
            gen2CollectionsDelta,
            _stopwatch.Elapsed.TotalMilliseconds);
    }
}

internal readonly record struct MemoryCheckpointSnapshot(
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
    double ElapsedMilliseconds);