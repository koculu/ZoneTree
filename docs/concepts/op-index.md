# Operation Indexes

An operation index is a freshness token associated with a write. ZoneTree uses it to order writes for the same key when replaying logs, loading in-memory segments, or applying replicated operations.

## Per-Key Freshness

The operation index is not a global database timestamp. It matters when comparing operations for the same key.

This distinction is important:

* newer operation for key `A` should beat older operation for key `A`,
* operation indexes for unrelated keys do not define the shape of the whole database.

## Why It Exists

ZoneTree stores data across multiple layers. During recovery and replication, the engine may need to decide which operation wins for a key. The operation index gives ZoneTree a stable way to preserve the intended write order for that key.

## Persisted Segments

Persisted disk segment entries do not need to keep per-entry operation indexes in the same way mutable in-memory segments do. Once data is merged into a disk segment, segment ordering and merge rules carry the durable shape.

## Replication And Audit

Operation indexes are useful when building replication, audit, or replay pipelines. They help consumers reason about the relative freshness of writes for a key.

Do not design a distributed global clock around `opIndex`. Use it as ZoneTree intends: a producer freshness token for key-level conflict resolution.
