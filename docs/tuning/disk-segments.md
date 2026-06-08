# Disk Segment Tuning

Disk segment tuning controls the long-term shape of persistent data.

## Start With The Workload

Before tuning, identify whether the workload is dominated by:

* point reads,
* range scans,
* large sequential scans,
* heavy writes,
* deletes/TTL,
* large values,
* backup/restore constraints,
* file-size limits.

## Disk Segment Mode

`MultiPartDiskSegment` is the default and is usually the right choice for large datasets. It partitions large disk segments into parts and helps keep very large storage shapes manageable.

`SingleDiskSegment` can be appropriate for smaller databases where one segment file is easier to manage.

## Minimum And Maximum Record Count

For multipart disk segments:

* `MinimumRecordCount` controls the lower target size for a part,
* `MaximumRecordCount` controls the upper target size for a part.

Larger parts mean fewer files and potentially better sequential behavior. Smaller parts can reduce operational file-size pressure and make part-level reuse more flexible.

## Sparse Array Step Size

`DefaultSparseArrayStepSize` controls how frequently ZoneTree records sparse index entries while creating disk segments.

Trade-off:

| Step size | Effect |
| --- | --- |
| Smaller | more sparse entries, more memory, faster positioning |
| Larger | fewer sparse entries, less memory, more local search |
| `0` | disables default sparse array creation/loading |

Use a lower step size when point lookups and seeks dominate. Use a higher step size when memory pressure matters more.

## Fixed-Size Layouts

When keys and values are small unmanaged structs, ZoneTree can use fixed-size disk segment layouts. This can reduce metadata overhead and simplify disk access.

For variable-length values such as strings and byte arrays, ZoneTree uses layouts with offsets and headers.

## Compression Block Size

`CompressionBlockSize` affects disk compression and random-access behavior.

Larger blocks often compress better but can make small random reads more expensive. Smaller blocks can improve random read granularity but may reduce compression ratio.

## Cache Settings

Tune:

* `KeyCacheSize`
* `ValueCacheSize`
* key/value cache lifetimes
* iterator cache contribution

Read-heavy hot-key workloads may benefit from larger caches. One-off scans may be better when they do not pollute block cache.

## Practical Defaults

Default disk segment settings are designed to be reasonable for general workloads. Tune only after you know what is limiting the system:

* memory,
* disk IO,
* file count,
* merge duration,
* read latency,
* backup/restore time.
