# Storage Engine Model

ZoneTree is a storage engine for building databases and data platforms. It gives your application an ordered, durable, programmable data layer with the low-level controls needed to shape storage around the product.

ZoneTree can be used directly as an ordered key-value database, or as the storage foundation for:

* custom databases,
* indexes,
* search systems,
* queues,
* event stores,
* time-series storage,
* local-first data layers,
* partitioned and replicated platforms.

## What ZoneTree Owns

ZoneTree owns the low-level storage concerns:

* key ordering,
* writes and reads,
* write-ahead logging,
* in-memory segments,
* disk segments,
* merge and compaction,
* serializers and comparers,
* iteration,
* transactions,
* operation indexes.

## What Your System Composes

ZoneTree provides the storage-engine primitives for higher-level systems. Your system composes those primitives into its product model:

* schema,
* partitioning,
* replication,
* query language,
* index design,
* retention policy,
* operational workflow,
* user-facing semantics.

This split is intentional. ZoneTree is designed from the ground up as a storage-engine foundation for scalable systems.

## Single-Node Engine, Scalable Foundation

ZoneTree provides the ordered keyspace, durability controls, iterators, operation indexes, transactions, and maintenance hooks needed to build partitioned, replicated, or domain-specific data platforms above it.

Use one ZoneTree when one ordered keyspace is enough. Use multiple ZoneTrees when you want separate partitions, tenants, indexes, or data models.

## Why It Scales As A Foundation

ZoneTree exposes the pieces that larger systems need:

* independent ZoneTree instances for shards, tenants, indexes, or time buckets,
* operation indexes for per-key freshness during replay,
* snapshot and read-only iterators for export, rebuild, and backup pipelines,
* live backup and restore abstractions for moving complete generation data,
* custom WAL and random-access storage providers,
* maintenance events for segment lifecycle visibility,
* transactions when several local keys must commit together.

Those pieces keep the engine small and composable while still giving system builders direct access to the storage behaviors that matter.
