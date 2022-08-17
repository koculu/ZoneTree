#undef TRACE_ENABLED

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Tenray.ZoneTree.Core;
using Tenray.ZoneTree.Segments.Disk;

namespace Tenray.ZoneTree.Maintainers;

public sealed class BasicZoneTreeMaintainer<TKey, TValue> : IDisposable
{
    readonly ILogger Logger;

    volatile bool RestartMerge;

    readonly CancellationTokenSource PeriodicTimerCancellationTokenSource = new();

    public IZoneTree<TKey, TValue> ZoneTree { get; }

    public IZoneTreeMaintenance<TKey, TValue> Maintenance { get; }

    public int MinimumSparseArrayLength = 1_000;

    public int SparseArrayStepLength = 1_000;

    /// <summary>
    /// /// Starts merge operation
    /// if records count in readonly segments exceeds this value.
    /// </summary>
    public int ThresholdForMergeOperationStart = 2_000_000;

    /// <summary>
    /// Starts merge operation
    /// if readonly segments count exceeds this value.
    /// </summary>
    public int MaximumReadOnlySegmentCount = 64;

    public bool EnablePeriodicTimer { get; set; } = true;

    public long DiskSegmentBufferLifeTime = TimeSpan.FromSeconds(10).Ticks;
    
    public TimeSpan PeriodicTimerInterval { get; set; } = TimeSpan.FromSeconds(5);

    public ConcurrentDictionary<int, Thread> MergerThreads = new();

    public BasicZoneTreeMaintainer(IZoneTree<TKey, TValue> zoneTree, ILogger logger = null)
    {
        Logger = logger ?? zoneTree.Logger;
        ZoneTree = zoneTree;
        Maintenance = zoneTree.Maintenance;
        AttachEvents();
        if (EnablePeriodicTimer)
            Task.Run(StartPeriodicTimer);
    }

    public BasicZoneTreeMaintainer(ITransactionalZoneTree<TKey, TValue> zoneTree, ILogger logger = null)
    {
        Logger = logger ?? zoneTree.Logger;
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
        if (Maintenance.ReadOnlySegmentsCount > MaximumReadOnlySegmentCount)
            StartMerge();
        else if (Maintenance.ReadOnlySegmentsRecordCount > ThresholdForMergeOperationStart)
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
        }
    }

    public void CompleteRunningTasks()
    {
        while (true)
        {
            var threads = MergerThreads.ToArray();
            if (threads.Length == 0)
                return;
            Trace($"Waiting {threads.Length} merge threads");
            foreach (var a in threads)
            {
                var t = a.Value;
                if (t.ThreadState == ThreadState.Stopped)
                    MergerThreads.TryRemove(a.Key, out var _);
                else
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
        Logger.LogTrace(msg);
    }

    public void Dispose()
    {
        PeriodicTimerCancellationTokenSource.Cancel();
        Maintenance.OnSegmentZeroMovedForward -= OnSegmentZeroMovedForward;
        Maintenance.OnDiskSegmentCreated -= OnDiskSegmentCreated;
        Maintenance.OnMergeOperationEnded -= OnMergeOperationEnded;
    }
}
