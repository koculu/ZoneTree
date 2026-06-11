# Storage Engine Model

ZoneTree is a storage engine for building databases and data platforms. It gives your application an ordered, durable, programmable data layer. You decide what higher-level model lives above it.

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

## What Your System Owns

Your system owns the product model:

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
