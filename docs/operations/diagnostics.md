# Diagnostics

ZoneTree exposes enough operational shape to build diagnostics around memory, maintenance, and storage behavior.

## What To Observe

Track:

* `zoneTree.Maintenance.MutableSegmentRecordCount`,
* `zoneTree.Maintenance.ReadOnlySegmentsCount`,
* `zoneTree.Maintenance.ReadOnlySegmentsRecordCount`,
* `zoneTree.Maintenance.InMemoryRecordCount`,
* `zoneTree.Maintenance.TotalRecordCount`,
* `zoneTree.Maintenance.IsMerging`,
* `zoneTree.Maintenance.IsBottomSegmentsMerging`,
* `zoneTree.Maintenance.BottomSegments.Count`,
* merge start/end,
* WAL size,
* backup duration,
* iterator lifetime,
* process memory and live managed memory.

## Maintenance Signals

Maintenance events can be used to observe segment lifecycle. They are useful for logs, metrics, and operational dashboards.

Useful events include:

* `OnMutableSegmentMovedForward`,
* `OnMergeOperationStarted`,
* `OnMergeOperationEnded`,
* `OnBottomSegmentsMergeOperationStarted`,
* `OnBottomSegmentsMergeOperationEnded`,
* `OnDiskSegmentCreated`,
* `OnDiskSegmentActivated`,
* `OnCanNotDropReadOnlySegment`,
* `OnCanNotDropDiskSegment`,
* `OnCanNotDropDiskSegmentCreator`.

Example:

```csharp
zoneTree.Maintenance.OnMergeOperationEnded += (_, result) =>
{
    Console.WriteLine($"Merge ended: {result}");
};

zoneTree.Maintenance.OnMutableSegmentMovedForward += tree =>
{
    Console.WriteLine(
        $"Read-only segments: {tree.ReadOnlySegmentsCount}, " +
        $"in-memory records: {tree.InMemoryRecordCount}");
};
```

## Logs

Configure logging according to your application needs.

```csharp
using ZoneTree.Logger;

using var zoneTree = new ZoneTreeFactory<int, string>()
    .SetDataDirectory("data/app")
    .SetLogger(new ConsoleLogger(LogLevel.Info))
    .OpenOrCreate();
```

For production systems, capture:

* failed drops,
* failed merges,
* recovery errors,
* WAL corruption or incomplete-tail events,
* unusually long maintenance tasks.

## Memory Diagnostics

Use .NET tools when diagnosing memory:

* live object counts,
* LOH usage,
* allocation rate,
* GC pauses,
* retained objects,
* pinned objects.

OS process memory alone is not enough to prove live ZoneTree memory usage.

## Benchmark Diagnostics

When benchmarking, record:

* key/value type,
* serializers,
* comparer,
* WAL mode,
* compression settings,
* mutable segment size,
* disk segment mode,
* storage hardware,
* maintenance settings.
