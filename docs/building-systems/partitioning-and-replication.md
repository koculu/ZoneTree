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

```csharp
var tenantTree = new ZoneTreeFactory<string, byte[]>()
    .SetDataDirectory($"data/tenant-{tenantId}")
    .OpenOrCreate();
```

Use partitions when you want independent maintenance, backup, restore, or placement decisions. Do not partition only because the API supports it; partition when there is an operational boundary.

## Replication

Replication can be built above ZoneTree with application-level logs, WAL-derived streams, operation indexes, or domain events.

Operation indexes are useful as per-key freshness tokens. They are not a global distributed clock.

For replication, include enough metadata in your operation stream to make replay idempotent:

```text
partitionId
key
value or deletion marker
opIndex
source node
```

On replay, compare operation indexes only for the same key. For unrelated keys, use your replication layer's ordering and conflict rules.

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
