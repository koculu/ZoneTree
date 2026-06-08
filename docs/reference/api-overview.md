# API Overview

This page gives a quick map of the main API areas.

## Tree

`IZoneTree<TKey, TValue>` is the main non-transactional interface.

Important methods:

* `TryGet`
* `ContainsKey`
* `Upsert`
* `TryAdd`
* `TryDelete`
* `ForceDelete`
* `CreateIterator`
* `CreateReverseIterator`
* `CreateMaintainer`
* `Count`
* `CountFullScan`

## Atomic Methods

Atomic methods synchronize same-key read-modify-write operations across LSM-tree segments.

Important methods:

* `TryAtomicGetAndUpdate`
* `TryAtomicAdd`
* `TryAtomicUpdate`
* `TryAtomicAddOrUpdate`
* `AtomicUpsert`

Use them only when the next value depends on the current value.

## Transactions

Transactional trees coordinate multi-key changes.

Important areas:

* `BeginTransaction`
* `BeginFluentTransaction`
* read/write with transaction id
* `Prepare`
* `PrepareAndCommit`
* `Commit`
* `Rollback`
* `ReadCommittedTryGet`
* `UpsertAutoCommit`
* `DeleteAutoCommit`
* exceptionless transaction APIs,
* fluent transaction APIs.

## Maintenance

Maintenance APIs expose segment lifecycle operations and events.

Use the maintainer for standard background maintenance. Use low-level maintenance APIs when building custom storage services.

Important metrics:

* `MutableSegmentRecordCount`
* `ReadOnlySegmentsCount`
* `ReadOnlySegmentsRecordCount`
* `InMemoryRecordCount`
* `TotalRecordCount`
* `IsMerging`
* `IsBottomSegmentsMerging`

Important operations:

* `MoveMutableSegmentForward`
* `StartMergeOperation`
* `StartBottomSegmentsMergeOperation`
* `SaveMetaData`
* `ReleaseReadBuffers`
* `ReleaseCircularKeyCacheRecords`
* `ReleaseCircularValueCacheRecords`

## Iterators

Iterators scan in key order or reverse key order. Use `Seek` for range scans.

Dispose iterators promptly.

Iterator options:

* `IteratorType.AutoRefresh`
* `IteratorType.NoRefresh`
* `IteratorType.Snapshot`
* `IteratorType.ReadOnlyRegion`
* `includeDeletedRecords`
* `contributeToTheBlockCache`
