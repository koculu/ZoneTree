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

* begin transaction,
* read/write with transaction id,
* prepare,
* commit,
* exceptionless transaction APIs,
* fluent transaction APIs.

## Maintenance

Maintenance APIs expose segment lifecycle operations and events.

Use the maintainer for standard background maintenance. Use low-level maintenance APIs when building custom storage services.

## Iterators

Iterators scan in key order or reverse key order. Use `Seek` for range scans.

Dispose iterators promptly.
