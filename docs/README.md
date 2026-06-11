# ZoneTree Docs

ZoneTree is a high-performance LSM-tree storage engine for .NET. It provides ordered, persistent key-value storage that can be used directly or as the foundation for databases, indexes, queues, search systems, event stores, and custom data platforms.

These docs explain the mental model behind ZoneTree, the main APIs, and the operational choices that matter in production.

## Core Strengths

ZoneTree is strongest when it is treated as a storage-engine foundation: an ordered durable core for building higher-level data systems.

* Ordered keys make range scans, prefix layouts, secondary indexes, queues, and time-series layouts natural.
* The LSM-tree write path gives high-throughput persistent writes.
* Multipart disk segments reduce write amplification by keeping rewrite work local when ranges change.
* Operation indexes support per-key freshness for replay, audit, and replication pipelines.
* Iterators, live backup, restore, transactions, and maintenance hooks make ZoneTree useful as a building block for larger data systems.

For the storage model, start with [Storage engine model](concepts/storage-engine.md). For ZoneTree's merge design, read [Write amplification](tuning/write-amplification.md).

## Start here

* [Getting started](getting-started.md)
* [Storage engine model](concepts/storage-engine.md)
* [Reads and writes](usage/reads-and-writes.md)
* [Value mutability](concepts/value-mutability.md)
* [Iteration and range scans](usage/iteration-and-range-scans.md)
* [Maintenance](usage/maintenance.md)

## Concepts

* [LSM-tree model](concepts/lsm-tree.md)
* [Key ordering](concepts/key-ordering.md)
* [Deletion markers and TTL](concepts/deletion-markers-and-ttl.md)
* [Operation indexes](concepts/op-index.md)
* [Value mutability](concepts/value-mutability.md)

## Usage

* [Opening a tree](usage/opening-a-tree.md)
* [Reads and writes](usage/reads-and-writes.md)
* [Iteration and range scans](usage/iteration-and-range-scans.md)
* [Atomic operations](usage/atomic-operations.md)
* [Transactions](usage/transactions.md)
* [Maintenance](usage/maintenance.md)

## Durability

* [WAL modes](durability/wal-modes.md)
* [Recovery](durability/recovery.md)
* [Backups](durability/backups.md)

## Storage

* [Memory usage](storage/memory-usage.md)
* [Disk segments](storage/disk-segments.md)
* [Read-path caching](storage/read-path-caching.md)
* [Compression](storage/compression.md)
* [Serializers and comparers](storage/serializers-and-comparers.md)

## Tuning

* [Write-heavy workloads](tuning/write-heavy-workloads.md)
* [Write amplification](tuning/write-amplification.md)
* [Read-heavy workloads](tuning/read-heavy-workloads.md)
* [Disk segment tuning](tuning/disk-segments.md)
* [Large values](tuning/large-values.md)

## Build On ZoneTree

* [Indexes](building-systems/indexes.md)
* [Queues](building-systems/queues.md)
* [Event stores](building-systems/event-stores.md)
* [Time-series storage](building-systems/time-series.md)
* [Partitioning and replication](building-systems/partitioning-and-replication.md)

## Operations

* [Production checklist](operations/production-checklist.md)
* [Troubleshooting](operations/troubleshooting.md)
* [Diagnostics](operations/diagnostics.md)

## Reference

* [Configuration](reference/configuration.md)
* [API overview](reference/api-overview.md)
