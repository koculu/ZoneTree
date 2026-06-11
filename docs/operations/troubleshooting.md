# Troubleshooting

This page maps common symptoms to the ZoneTree subsystem that usually explains them.

Start with measurements. Most ZoneTree issues are easier to understand when you know whether pressure is coming from the mutable segment, read-only in-memory segments, disk segments, WAL, iterators, maintenance, or transactions.

## First Signals To Check

Useful maintenance counters:

* `zoneTree.Maintenance.MutableSegmentRecordCount`
* `zoneTree.Maintenance.ReadOnlySegmentsCount`
* `zoneTree.Maintenance.ReadOnlySegmentsRecordCount`
* `zoneTree.Maintenance.InMemoryRecordCount`
* `zoneTree.Maintenance.TotalRecordCount`
* `zoneTree.Maintenance.IsMerging`
* `zoneTree.Maintenance.IsBottomSegmentsMerging`
* `zoneTree.Maintenance.BottomSegments.Count`

Useful events:

* `OnMutableSegmentMovedForward`
* `OnMergeOperationStarted`
* `OnMergeOperationEnded`
* `OnBottomSegmentsMergeOperationStarted`
* `OnBottomSegmentsMergeOperationEnded`
* `OnCanNotDropReadOnlySegment`
* `OnCanNotDropDiskSegment`
* `OnCanNotDropDiskSegmentCreator`

Useful logs:

* merge failures,
* failed drop events,
* WAL read errors,
* recovery errors,
* live backup errors.

## Database Does Not Open

Common causes:

* the database does not exist and `Open()` was used,
* the database already exists and `Create()` was used,
* the key or value type does not match the stored metadata,
* the comparer type does not match the stored metadata,
* the key or value serializer type does not match the stored metadata,
* the existing database version is not compatible,
* the meta WAL or segment WAL records are corrupted.

What to do:

* verify the data directory and WAL directory,
* open with the same key type, value type, comparer, and serializers used when the database was created,
* do not change comparer semantics in-place,
* check logs for WAL read or metadata errors.

An incomplete WAL tail can happen after process termination. ZoneTree loaders can detect and truncate a single incomplete tail record in the mutable/read-only WAL path. Other WAL errors are treated as corruption and should be investigated before reusing the files.

## Writes Slow Down

Likely causes:

* WAL mode or storage latency is dominating writes,
* serializers or value copies are expensive,
* values are large,
* mutable segments are moving too often,
* read-only segments are accumulating because maintenance is behind,
* atomic or transactional APIs are being used where plain `Upsert` would be enough.

What to inspect:

* `ReadOnlySegmentsCount`
* `ReadOnlySegmentsRecordCount`
* `IsMerging`
* merge result events,
* WAL mode and compression settings,
* value size and serializer cost.

What to do:

* keep a maintainer alive for long-running write-heavy workloads,
* tune `MutableSegmentMaxItemCount` for record size,
* use `Upsert` for simple inserts and replacements,
* use atomic methods only when the new value depends on the current value,
* use transactions only when multiple keys must be coordinated,
* benchmark WAL modes with the real storage device.

## Read-Only Segments Accumulate

Read-only segments are frozen in-memory segments waiting to be merged into disk segments.

Likely causes:

* no maintainer is running,
* merge work is slower than the write rate,
* another merge is already running,
* read-only segments are not fully frozen yet,
* merge was cancelled,
* merge failed.

What to inspect:

* `ReadOnlySegmentsCount`
* `ReadOnlySegmentsRecordCount`
* `IsMerging`
* `OnMergeOperationEnded`

Merge results to look for:

* `SUCCESS`: merge completed,
* `RETRY_READONLY_SEGMENTS_ARE_NOT_READY`: merge can be retried by the maintainer,
* `ANOTHER_MERGE_IS_RUNNING`: a merge request arrived while another merge was active,
* `NOTHING_TO_MERGE`: there were no read-only segments to merge,
* `CANCELLED_BY_USER`: cancellation was requested,
* `FAILURE`: check the logger.

What to do:

* keep `zoneTree.CreateMaintainer()` alive while the tree is active,
* lower mutable segment size if memory pressure is high,
* inspect merge failures in logs,
* call `WaitForBackgroundThreads()` during controlled shutdown.

## Reads Slow Down

Likely causes:

* too many segments must be searched,
* key layout does not match the read pattern,
* sparse arrays are too sparse or disabled,
* decompressed block cache lifetime is too short for the working set,
* key/value circular caches are too small for repeated same-record reads,
* compression CPU cost is high,
* full scans are affecting the cache strategy.

What to inspect:

* read-only segment count,
* bottom segment count,
* disk segment mode,
* `DefaultSparseArrayStepSize`,
* maintainer `BlockCacheLifeTime`,
* maintainer `InactiveBlockCacheCleanupInterval`,
* key/value cache sizes and lifetimes,
* whether iterators contribute to the block cache.

What to do:

* keep maintenance healthy,
* design keys around range scans and locality,
* tune sparse array density after measuring reads,
* tune block cache lifetime for repeated nearby disk reads,
* tune circular key/value caches for repeated same-record reads,
* keep one-off full scans from contributing to the block cache.

## Process Memory Looks High

High process memory does not always mean ZoneTree is holding that much live data. .NET may keep freed memory available for reuse instead of returning it to the operating system immediately.

Likely ZoneTree contributors:

* active mutable segment,
* read-only segments waiting for merge,
* large keys or values,
* decompressed disk block cache,
* circular key/value caches,
* iterators that pin segments,
* temporary merge and WAL buffers.

What to inspect:

* live managed objects with .NET diagnostics,
* `MutableSegmentRecordCount`,
* `ReadOnlySegmentsCount`,
* iterator lifetimes,
* value shape and value size,
* maintainer block cache lifetime and cleanup interval,
* maintainer activity.

What to do:

* tune `MutableSegmentMaxItemCount` by expected byte size, not only record count,
* keep maintenance running,
* shorten `BlockCacheLifeTime` if inactive disk blocks are retained too long,
* dispose iterators promptly,
* prefer immutable value shapes,
* use .NET memory diagnostics instead of relying only on OS process memory.

## Disk Usage Grows

ZoneTree is an LSM-tree. Old versions, deletion markers, and replaced segment files can remain until compaction and cleanup remove them.

Likely causes:

* obsolete records are waiting for merge,
* deletion markers or TTL-expired records are waiting for compaction,
* bottom segments are accumulating,
* WAL files are large because in-memory data has not been merged yet,
* backup generations are retained,
* old segment or WAL files could not be dropped.

What to inspect:

* merge activity,
* bottom segment count,
* WAL directory size,
* backup retention,
* `OnCanNotDropReadOnlySegment`,
* `OnCanNotDropDiskSegment`,
* `OnCanNotDropDiskSegmentCreator`.

Failed drop events do not mean the database is corrupted. They mean cleanup could not delete a file or temporary creator at that moment. Keep the logs and investigate the underlying file-system exception.

## Deleted Records Still Appear

Normal read APIs hide deleted records.

Deleted records can still appear when:

* using low-level scans with `includeDeletedRecords: true`,
* inspecting old segment files,
* data has not yet been compacted.

What to do:

* use normal read APIs for live-record semantics,
* use `includeDeletedRecords: true` only for diagnostics or low-level tooling,
* keep maintenance running so obsolete records can be removed during compaction.

## Count Looks Higher Than Expected

`TotalRecordCount` is a storage-shape counter, not a unique live-record count. In an LSM-tree, the same key can exist in multiple layers until compaction removes older versions.

Use:

* `Count()` or `CountFullScan()` when you need live-record counts,
* maintenance counters when you need storage pressure diagnostics.

## Recovery Is Slow

Likely causes:

* large WAL files,
* many unmerged read-only segments,
* slow storage,
* compression cost,
* single-segment garbage collection work.

What to do:

* keep maintenance healthy before shutdown,
* wait for background work during controlled shutdown,
* avoid very large mutable segments unless memory and recovery time are acceptable,
* review WAL mode and compression settings.

## Recovery Fails

Common causes:

* metadata type/comparer/serializer mismatch,
* incompatible database version,
* meta WAL corruption,
* segment WAL corruption,
* missing segment files,
* storage/provider path mismatch.

What to do:

* reopen with the same factory configuration,
* verify data and WAL directories,
* check logs for the first WAL or metadata error,
* restore from backup if WAL corruption is confirmed.

## Transactions Are Slow Or Stay Pending

Transactions coordinate multiple keys and are heavier than regular writes.

Likely causes:

* transactions are long-lived,
* transactions depend on pending transactions,
* abandoned transaction ids are still present,
* auto-commit helpers are used for write paths that do not need transaction logging.

What to inspect:

* `CommitResult.IsPendingTransactions`,
* `PendingTransactionList`,
* `transactionalZoneTree.Maintenance.UncommittedTransactionIds`.

What to do:

* keep transactions short,
* use non-transactional `Upsert` for simple writes,
* use atomic methods for same-key read-modify-write,
* roll back abandoned transaction ids with `RollbackUncommittedTransactionIdsBefore(...)` during operational cleanup.

## Live Backup Generation Is Skipped

Scheduled and merge-triggered generations are opportunistic. If another generation is already running, ZoneTree skips the new request instead of queueing backup work.

Use `CreateGenerationAsync()` when a generation must be created immediately and the caller should see a failure if backup is busy.

## Live Backup Restore Fails Because Target Exists

Restore expects an empty target ZoneTree directory. If the target already contains ZoneTree metadata, restore throws `LiveBackupRestoreTargetAlreadyExistsException`.

Restore into a fresh data directory, or intentionally remove the old target outside ZoneTree before restoring.

## Local Backup Retention Does Not Delete A Segment File

Segment files are immutable and may be reused by newer generations. Local retention deletes a file only when no retained generation references its backup path.

Check the retained generation catalogs before assuming the file is leaked.

## Custom Backup Store Behaves Inconsistently

When `MaxConcurrentFileTransfers` is greater than `1`, `UseSegmentAsync` and `UploadSegmentFileAsync` may run concurrently for files in the same generation.

Make the custom store safe for concurrent segment-file operations, or configure `MaxConcurrentFileTransfers = 1`.

## Restore From Custom Source Has Missing Or Wrong Segments

Custom sources must preserve the `Order` value of each `DiskSegmentFile`. Restore uses that order to rebuild the active disk segment and bottom segment layout.
