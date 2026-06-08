# Production Checklist

Use this checklist before putting ZoneTree behind production traffic.

## Storage

* Choose the data directory intentionally.
* Ensure the storage volume has enough free space.
* Start with the default async compressed WAL for persistent data.
* Use sync WAL modes only when synchronous WAL acknowledgment is required.
* Use `No WAL` only for cache, temporary, or intentionally rebuildable data.
* Choose disk segment mode based on database size and operational file-size needs.
* Tune sparse array and cache settings only after measuring read behavior.
* Plan backup and restore.

## Memory

* Estimate key and value sizes.
* Tune `MutableSegmentMaxItemCount` for value size.
* Keep maintenance running for long-lived write-heavy applications.
* Avoid long-lived iterators unless they are deliberate.
* Use .NET diagnostics to measure live memory.

## Data Model

* Choose key layout for the dominant reads.
* Choose serializers and comparers before creating the database.
* Treat stored values as immutable snapshots.
* Prefer small immutable structs or readonly record structs when they fit.
* Define deletion or TTL behavior.
* Decide whether secondary indexes are transactional or rebuildable.

## Operations

* Test restart and recovery.
* Test backup restore.
* Watch disk growth.
* Watch read-only segment accumulation.
* Watch merge duration.
* Keep logs from failed maintenance/drop operations.

## API Use

* Use `Upsert` for simple writes.
* Use atomic methods only for same-key read-modify-write.
* Use transactions only for multi-key coordination.
* Dispose trees, maintainers, and iterators.
