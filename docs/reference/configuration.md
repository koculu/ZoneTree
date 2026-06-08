# Configuration

This page summarizes the most important configuration areas.

## Factory

`ZoneTreeFactory<TKey, TValue>` configures and opens a tree.

Common methods:

* `SetDataDirectory`
* `SetWriteAheadLogDirectory`
* `SetComparer`
* `SetKeySerializer`
* `SetValueSerializer`
* `SetMutableSegmentMaxItemCount`
* `SetDiskSegmentMaxItemCount`
* `SetIsDeletedDelegate`
* `SetMarkValueDeletedDelegate`
* `DisableDeletion`
* `OpenOrCreate`
* `Open`
* `Create`
* `OpenOrCreateTransactional`

## Memory

`MutableSegmentMaxItemCount` controls when the active mutable segment is moved forward.

Lower this for large values. Raise it only when memory budget and maintenance behavior are understood.

## Disk

Disk segment options affect file layout, compression, caches, sparse arrays, multipart sizing, and merge behavior.

Tune disk options with the actual read/write pattern.

Important options:

| Option | Purpose |
| --- | --- |
| `DiskSegmentMode` | single or multipart disk segment shape |
| `CompressionBlockSize` | block size for compressed random-access disk data |
| `CompressionMethod` | disk segment compression method |
| `CompressionLevel` | compression level for the selected method |
| `MinimumRecordCount` | lower target part size for multipart disk segments |
| `MaximumRecordCount` | upper target part size for multipart disk segments |
| `DefaultSparseArrayStepSize` | sparse index density for disk search |
| `KeyCacheSize` | circular cache size for recently read keys |
| `ValueCacheSize` | circular cache size for recently read values |
| `KeyCacheRecordLifeTimeInMillisecond` | key cache record lifetime |
| `ValueCacheRecordLifeTimeInMillisecond` | value cache record lifetime |

## WAL

WAL options control durability, compression, and backup behavior.

Choose WAL mode based on whether recent data can be reconstructed.

## Deletion

Deletion behavior is configured with:

* `SetIsDeletedDelegate`,
* `SetMarkValueDeletedDelegate`,
* `DisableDeletion`.

TTL can be modeled through custom deletion logic.
