#undef TRACE_ENABLED

using System.Collections.Concurrent;
using Tenray.ZoneTree.Core;
using Tenray.ZoneTree.Segments.Disk;

namespace Tenray.ZoneTree.Maintainers;

public sealed class BasicZoneTreeMaintainer<TKey, TValue> : IDisposable
{
    public IZoneTree<TKey, TValue> ZoneTree { get; }

    public IZoneTreeMaintenance<TKey, TValue> Maintenance { get; }

    public int MinimumSparseArrayLength = 1_000;

    public int SparseArrayStepLength = 1_000;

    public int ThresholdForMergeOperationStart = 2_000_000;

    public bool EnablePeriodicTimer { get; set; } = false;

    public long DiskSegmentBufferLifeTime = TimeSpan.FromSeconds(10).Ticks;
    
    public TimeSpan PeriodicTimerInterval { get; set; } = TimeSpan.FromSeconds(5);

    public ConcurrentDictionary<int, Thread> MergerThreads = new();

    public BasicZoneTreeMaintainer(IZoneTree<TKey, TValue> zoneTree)
    {
        ZoneTree = zoneTree;
        Maintenance = zoneTree.Maintenance;
        AttachEvents();
        if (EnablePeriodicTimer)
            Task.Run(StartPeriodicTimer);
    }

    public BasicZoneTreeMaintainer(ITransactionalZoneTree<TKey, TValue> zoneTree)
    {
        ZoneTree = zoneTree.Maintenance.ZoneTree;
        Maintenance = ZoneTree.Maintenance;
        AttachEvents();
        Task.Run(StartPeriodicTimer);
    }

    void AttachEvents()
    {
        Maintenance.OnSegmentZeroMovedForward += OnSegmentZeroMovedForward;
        Maintenance.OnDiskSegmentCreated += OnDiskSegmentCreated;
        Maintenance.OnMergeOperationEnded += OnMergeOperationEnded;
    }

    volatile bool RestartMerge;

    CancellationTokenSource PeriodicTimerCancellationTokenSource = new();

    void OnMergeOperationEnded(
        IZoneTreeMaintenance<TKey, TValue> zoneTree,
        MergeResult mergeResult)
    {
        switch (mergeResult)
        {
            case MergeResult.RETRY_READONLY_SEGMENTS_ARE_NOT_READY:
                Trace("RETRY_READONLY_SEGMENTS_ARE_NOT_READY");
                RestartMerge = false;
                StartMerge();
                break;
            case MergeResult.ANOTHER_MERGE_IS_RUNNING:
                Trace("ANOTHER_MERGE_IS_RUNNING");
                RestartMerge = true;
                break;
            case MergeResult.NOTHING_TO_MERGE:
                Trace("NOTHING_TO_MERGE");
                break;
            case MergeResult.CANCELLED_BY_USER:
                Trace("CANCELLED_BY_USER");
                break;
            case MergeResult.SUCCESS:
                Trace("MERGE SUCCESS");
                if (RestartMerge)
                {
                    Trace("Restarting merge");
                    RestartMerge = false;
                    StartMerge();
                }
                break;
            case MergeResult.FAILURE:
                Trace("MERGE FAILURE");
                break;
        }
        MergerThreads.Remove(Environment.CurrentManagedThreadId, out _);
    }

    void OnDiskSegmentCreated(IZoneTreeMaintenance<TKey, TValue> zoneTree, IDiskSegment<TKey, TValue> newDiskSegment)
    {
        var sparseArraySize = newDiskSegment.Length / SparseArrayStepLength;
        newDiskSegment.InitSparseArray(Math.Min(MinimumSparseArrayLength, sparseArraySize));
    }

    void OnSegmentZeroMovedForward(IZoneTreeMaintenance<TKey, TValue> zoneTree)
    {
        if (Maintenance.ReadOnlySegmentsRecordCount > ThresholdForMergeOperationStart)
            StartMerge();
    }

    void StartMerge()
    {
        lock (this)
        {
            var mergerThread = Maintenance.StartMergeOperation();
            if (mergerThread == null)
                return;
            MergerThreads.AddOrUpdate(
                mergerThread.ManagedThreadId,
                mergerThread,
                (key, value) => mergerThread);
            Trace("started merge");
        }
    }

    public void CompleteRunningTasks()
    {
        while (true)
        {
            var threads = MergerThreads.Values.ToArray();
            if (threads.Length == 0)
                return;
            Trace($"Waiting {threads.Length} merge threads");
            foreach (var t in threads)
            {
                t.Join();
            }
            Trace("Wait ended");
        }
    }

    async Task StartPeriodicTimer()
    {
        var cts = PeriodicTimerCancellationTokenSource;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(cts.Token))
        {
            var ticks = DateTime.UtcNow.Ticks - DiskSegmentBufferLifeTime;
            var releasedCount = ZoneTree.Maintenance.DiskSegment.ReleaseReadBuffers(ticks);
            Trace("Released Buffers: " + releasedCount);
        }
    }

    void Trace(string msg)
    {
#if TRACE_ENABLED
        Console.WriteLine(msg + " tid: " + Environment.CurrentManagedThreadId);
#endif
    }

    public void Dispose()
    {
        PeriodicTimerCancellationTokenSource.Cancel();
        Maintenance.OnSegmentZeroMovedForward -= OnSegmentZeroMovedForward;
        Maintenance.OnDiskSegmentCreated -= OnDiskSegmentCreated;
        Maintenance.OnMergeOperationEnded -= OnMergeOperationEnded;
    }
}
