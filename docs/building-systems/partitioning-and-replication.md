# Partitioning And Replication

ZoneTree is not a distributed database by itself. It is designed as a storage-engine foundation for systems that need partitioning or replication above the engine.

## Partitioning

You can partition data across multiple ZoneTrees:

```text
tenant shard -> ZoneTree
hash partition -> ZoneTree
time bucket -> ZoneTree
index family -> ZoneTree
```

Partitioning helps control:

* file size,
* memory pressure,
* backup windows,
* maintenance cost,
* tenant isolation,
* operational blast radius.

## Replication

Replication can be built above ZoneTree with application-level logs, WAL-derived streams, operation indexes, or domain events.

Operation indexes are useful as per-key freshness tokens. They are not a global distributed clock.

## Consistency

The replication layer owns:

* transport,
* retries,
* ordering,
* idempotency,
* conflict rules,
* failover,
* recovery.

ZoneTree owns local ordered durable storage.

## Recommended Approach

Start simple:

* define partition keys,
* define source-of-truth ownership,
* make writes idempotent,
* record operation metadata,
* test replay and recovery,
* add distribution only where needed.
