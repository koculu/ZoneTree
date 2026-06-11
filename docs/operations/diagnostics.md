# Diagnostics

Good ZoneTree diagnostics start with the shape of the LSM tree. Most operational questions can be answered by watching how records move through the mutable segment, read-only segments, disk segment, bottom segments, WAL files, and caches.

## Core Counters

Start with these maintenance counters:

| Counter | What It Tells You |
| --- | --- |
| `MutableSegmentRecordCount` | records currently in the writable mutable segment |
| `ReadOnlySegmentsCount` | number of frozen in-memory segments waiting for merge |
| `ReadOnlySegmentsRecordCount` | records waiting in read-only in-memory segments |
| `InMemoryRecordCount` | mutable plus read-only segment records |
| `TotalRecordCount` | total segment records, including duplicated historical records across LSM layers |
| `IsMerging` | normal merge is running |
| `IsBottomSegmentsMerging` | bottom segment merge is running |
| `BottomSegments.Count` | number of bottom disk segments |

`TotalRecordCount` is not the unique logical row count. LSM layers can contain older versions and deletion markers until merges remove them. Use scan-based counting when the exact live row count matters.

## Maintenance Events

Maintenance events are the best low-cost way to build logs and metrics around segment movement:

* `OnMutableSegmentMovedForward`
* `OnMergeOperationStarted`
* `OnMergeOperationEnded`
* `OnBottomSegmentsMergeOperationStarted`
* `OnBottomSegmentsMergeOperationEnded`
* `OnDiskSegmentCreated`
* `OnDiskSegmentActivated`
* `OnCanNotDropReadOnlySegment`
* `OnCanNotDropDiskSegment`
* `OnCanNotDropDiskSegmentCreator`

Example:

```csharp
zoneTree.Maintenance.OnMergeOperationEnded += (_, result) =>
{
    Console.WriteLine($"Merge ended: {result}");
};

zoneTree.Maintenance.OnMutableSegmentMovedForward += tree =>
{
    Console.WriteLine(
        $"read-only segments={tree.ReadOnlySegmentsCount}, " +
        $"in-memory records={tree.InMemoryRecordCount}");
};
```

Failed drop events do not mean the database is corrupt. They usually mean a file or segment is still referenced. ZoneTree can continue, and cleanup can happen later.

## Write Pressure

Symptoms:

* `ReadOnlySegmentsCount` keeps growing,
* `ReadOnlySegmentsRecordCount` keeps growing,
* memory grows with write volume,
* merges run often or for a long time.

Check:

* whether a maintainer is running,
* `MaximumReadOnlySegmentCount`,
* `ThresholdForMergeOperationStart`,
* `MutableSegmentMaxItemCount`,
* disk write throughput,
* value size.

For large values, a default `MutableSegmentMaxItemCount` of `1_000_000` records can be too high. Lower it so the mutable segment moves forward before the process holds too much live value data.

See [large values](../tuning/large-values.md) and [write-heavy workloads](../tuning/write-heavy-workloads.md).

## Merge Pressure

Symptoms:

* normal merge is almost always active,
* bottom segments accumulate,
* disk usage grows faster than live data,
* foreground writes are fine but background work never catches up.

Check:

* merge result events,
* read-only segment count and record count,
* disk segment max item count,
* multipart minimum and maximum record count,
* bottom segment count,
* long-lived iterators that may delay segment disposal.

Multipart disk segments reduce rewrite work for localized merges, but they do not remove the cost of wide random overlap. If incoming read-only segments touch most of the keyspace, many parts can be affected.

See [write amplification](../tuning/write-amplification.md).

## Read Path And Cache Behavior

For compressed disk segments, ZoneTree reads compressed blocks and keeps decompressed blocks in an internal block cache. The maintainer cleanup job releases inactive blocks based on `BlockCacheLifeTime`.

Symptoms:

* repeated disk reads are slower than expected,
* memory grows during read-heavy workloads,
* full scans disturb random-read performance.

Check:

* whether `CreateMaintainer()` is used in long-lived applications,
* `BlockCacheLifeTime`,
* `InactiveBlockCacheCleanupInterval`,
* disk segment compression block size,
* iterator `contributeToTheBlockCache`.

One-off scans should usually avoid contributing to the block cache. Repeated scans over a useful working set can enable cache contribution intentionally.

See [read path caching](../storage/read-path-caching.md).

## WAL And Recovery Signals

Track WAL size and recovery logs for persistent databases.

Symptoms:

* startup recovery takes longer than expected,
* WAL files grow,
* recovery reports incomplete tails,
* recovery reports checksum or deserialization failures.

Check:

* WAL mode,
* whether maintenance is merging read-only segments,
* whether incremental backup is intentionally enabled,
* serializer compatibility,
* storage errors.

An incomplete tail after a process crash is different from checksum or deserialization failure. Valid records before the incomplete tail can still be used. Checksum and deserialization failures should be investigated as data-integrity issues.

See [recovery](../durability/recovery.md) and [WAL modes](../durability/wal-modes.md).

## Backup Signals

For live backup, watch:

* generation duration,
* failed generation logs,
* file transfer duration,
* record batch size,
* local retention behavior if using `LocalLiveBackupProvider`,
* restore tests from real generations.

Live backup is generation based. A generation contains the disk segment files and optional in-memory record batch needed for that backup point. Restore should be tested before production traffic depends on it.

See [backups](../durability/backups.md).

## Memory Diagnostics

OS process memory does not equal live ZoneTree data. .NET can retain memory for future allocations even after objects become collectible.

Use .NET diagnostics when memory matters:

* live managed object size,
* allocation rate,
* large object heap usage,
* GC pauses,
* retained references,
* long-lived iterators,
* mutable segment size,
* block cache lifetime.

The most common ZoneTree-side memory levers are:

* `MutableSegmentMaxItemCount`,
* value size,
* maintainer cleanup,
* block cache lifetime,
* iterator lifetime.

## Logging

Configure a logger when running in production:

```csharp
using ZoneTree.Logger;

using var zoneTree = new ZoneTreeFactory<int, string>()
    .SetDataDirectory("data/app")
    .SetLogger(new ConsoleLogger(LogLevel.Info))
    .OpenOrCreate();
```

Capture at least:

* failed merges,
* failed drops,
* WAL read errors,
* recovery warnings,
* backup failures,
* unusually long maintenance operations.

## Benchmark Records

When comparing results, record the full shape:

* key and value type,
* serializers,
* comparer,
* WAL mode,
* disk segment mode,
* compression settings,
* mutable segment size,
* multipart min/max record count,
* disk segment max item count,
* block cache lifetime,
* storage hardware,
* maintenance settings.

Without those details, benchmark numbers are difficult to explain or reproduce.
