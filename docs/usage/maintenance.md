# Maintenance

The maintainer is the normal way to run ZoneTree maintenance in a service. Keep
one maintainer alive for each active tree so merge work and inactive cache
cleanup happen in the background.

```csharp
using var zoneTree = new ZoneTreeFactory<int, string>()
    .SetDataDirectory("data/app")
    .OpenOrCreate();

using var maintainer = zoneTree.CreateMaintainer();
```

`CreateMaintainer()` attaches to the tree's maintenance events. After writes move
the mutable segment forward, the maintainer decides whether to start a merge. It
also starts the periodic cleanup job for disk-read caches.

For most applications, this is the whole maintenance setup.

## Merge Triggers

The default maintainer starts a normal merge after the mutable segment moves
forward and either trigger is reached:

| Setting | Default | Meaning |
| --- | ---: | --- |
| `ThresholdForMergeOperationStart` | `0` records | merge when read-only records exceed this count |
| `MaximumReadOnlySegmentCount` | `64` | merge when read-only segment count exceeds this count |

With the default threshold of `0`, any non-empty read-only layer can start a
merge after segment movement.

```csharp
maintainer.ThresholdForMergeOperationStart = 500_000;
maintainer.MaximumReadOnlySegmentCount = 32;
```

Use a higher record threshold when you want larger merge batches. Use a lower
read-only segment count limit when memory pressure should trigger merge work
sooner.

## Evict Current Data

`EvictToDisk()` moves the current mutable segment forward and starts a normal
merge.

```csharp
maintainer.EvictToDisk();
maintainer.WaitForBackgroundThreads();
```

Use it before controlled shutdowns, exports, or explicit maintenance points when
you want the current in-memory records to enter the merge pipeline immediately.
Call `WaitForBackgroundThreads()` when the caller needs the merge to finish
before continuing.

## Waiting And Cancellation

The maintainer tracks the merge threads it starts.

```csharp
maintainer.WaitForBackgroundThreads();
```

For async callers:

```csharp
await maintainer.WaitForBackgroundThreadsAsync();
```

To request a faster shutdown:

```csharp
maintainer.TryCancelBackgroundThreads();
maintainer.WaitForBackgroundThreads();
```

`TryCancelBackgroundThreads()` asks active normal and bottom-segment merges to
cancel. The merge threads finish when they observe the cancellation request.

## Cache Cleanup

The default maintainer starts inactive cache cleanup automatically.

| Setting | Default |
| --- | ---: |
| `EnableJobForCleaningInactiveCaches` | `true` |
| `BlockCacheLifeTime` | `1 minute` |
| `InactiveBlockCacheCleanupInterval` | `30 seconds` |

```csharp
maintainer.BlockCacheLifeTime = TimeSpan.FromMinutes(2);
maintainer.InactiveBlockCacheCleanupInterval = TimeSpan.FromSeconds(30);
```

Longer cache lifetime can help repeated disk reads. Shorter cache lifetime
reduces retained read-cache memory. The cleanup job releases inactive
decompressed blocks and expired circular key/value cache records.

For read-cache details, see [read-path caching](../storage/read-path-caching.md).

## Bottom Segment Merge

Bottom segment merge is an explicit operation. Run it when your service wants to
compact a range of bottom segments.

```csharp
maintainer.StartBottomSegmentsMerge();
maintainer.WaitForBackgroundThreads();
```

To merge a selected range:

```csharp
maintainer.StartBottomSegmentsMerge(fromIndex: 0, toIndex: 4);
maintainer.WaitForBackgroundThreads();
```

The range uses the current bottom segment order. A broad range such as
`0..int.MaxValue` asks ZoneTree to merge as much of the bottom layer as possible.

## Direct Maintenance API

`zoneTree.Maintenance` exposes the lower-level operations used by the maintainer.
Use it when your application owns its own scheduler.

```csharp
zoneTree.Maintenance.MoveMutableSegmentForward();

var thread = zoneTree.Maintenance.StartMergeOperation();
thread?.Join();
```

Useful direct operations:

| Operation | Purpose |
| --- | --- |
| `MoveMutableSegmentForward()` | move the current mutable segment into the read-only layer |
| `StartMergeOperation()` | start a normal merge thread |
| `StartBottomSegmentsMergeOperation(fromIndex, toIndex)` | start a bottom-segment merge thread |
| `TryCancelMergeOperation()` | request cancellation for the active normal merge |
| `TryCancelBottomSegmentsMergeOperation()` | request cancellation for the active bottom-segment merge |
| `SaveMetaData()` | refresh the JSON metadata file and clear pending metadata records |
| `ReleaseReadBuffers(ticks)` | release inactive decompressed disk blocks |
| `ReleaseCircularKeyCacheRecords()` | release expired key-cache records |
| `ReleaseCircularValueCacheRecords()` | release expired value-cache records |

`StartMergeOperation()` and `StartBottomSegmentsMergeOperation(...)` return the
created thread, or `null` when a merge of the same kind is already active.

## Merge Results

Merge completion is reported through maintenance events.

```csharp
zoneTree.Maintenance.OnMergeOperationEnded += (_, result) =>
{
    Console.WriteLine(result);
};
```

| Result | Meaning |
| --- | --- |
| `SUCCESS` | merge completed |
| `NOTHING_TO_MERGE` | no eligible read-only segments were available |
| `ANOTHER_MERGE_IS_RUNNING` | a merge of the same kind was already active |
| `RETRY_READONLY_SEGMENTS_ARE_NOT_READY` | read-only segments were still preparing |
| `CANCELLED_BY_USER` | cancellation was requested |
| `FAILURE` | an exception occurred; inspect the logger |

The default maintainer retries `RETRY_READONLY_SEGMENTS_ARE_NOT_READY`. If it sees
`ANOTHER_MERGE_IS_RUNNING`, it starts another merge after the active merge
finishes.

## Counters

Maintenance counters are useful for dashboards and health checks.

| Counter | Meaning |
| --- | --- |
| `MutableSegmentRecordCount` | records in the current mutable segment |
| `ReadOnlySegmentsCount` | read-only in-memory segment count |
| `ReadOnlySegmentsRecordCount` | records across read-only in-memory segments |
| `InMemoryRecordCount` | mutable plus read-only record count |
| `TotalRecordCount` | physical records across memory and disk layers |
| `IsMerging` | normal merge is active |
| `IsBottomSegmentsMerging` | bottom-segment merge is active |

`TotalRecordCount` is a physical storage counter. Use `Count()` or
`CountFullScan()` for live-record counts.

## Events

Use events for monitoring, scheduling, and cleanup reporting.

| Event | Use |
| --- | --- |
| `OnMutableSegmentMovedForward` | observe mutable segment movement |
| `OnMergeOperationStarted` | observe normal merge start |
| `OnMergeOperationEnded` | observe normal merge result |
| `OnBottomSegmentsMergeOperationStarted` | observe bottom merge start |
| `OnBottomSegmentsMergeOperationEnded` | observe bottom merge result |
| `OnDiskSegmentCreated` | observe created disk segment files |
| `OnDiskSegmentActivated` | observe the active disk segment change |
| `OnCanNotDropReadOnlySegment` | report cleanup failure for read-only segment files |
| `OnCanNotDropDiskSegment` | report cleanup failure for disk segment files |
| `OnCanNotDropDiskSegmentCreator` | report cleanup failure for unfinished merge output |

Failed drop events mean obsolete files or temporary output stayed behind after a
cleanup attempt failed. Log the exception and investigate the file-system or
provider error.

## Iterator Lifetime

Dispose iterators as soon as scans finish. Long-lived iterators can keep segments
alive, which delays cleanup of old segment files and read buffers.

Snapshot iterators move the mutable segment forward when they are created. Heavy
snapshot-iterator usage under write load can increase read-only segment pressure.

## Practical Patterns

For a normal service:

```csharp
using var zoneTree = new ZoneTreeFactory<int, string>()
    .SetDataDirectory("data/app")
    .OpenOrCreate();

using var maintainer = zoneTree.CreateMaintainer();

// run application

maintainer.WaitForBackgroundThreads();
```

For a controlled checkpoint:

```csharp
maintainer.EvictToDisk();
maintainer.WaitForBackgroundThreads();
zoneTree.Maintenance.SaveMetaData();
```

For a custom maintenance window:

```csharp
maintainer.StartMerge();
maintainer.StartBottomSegmentsMerge();
maintainer.WaitForBackgroundThreads();
```

For related tuning, see [memory usage](../storage/memory-usage.md),
[disk segment tuning](../tuning/disk-segments.md), and
[read-path caching](../storage/read-path-caching.md).
