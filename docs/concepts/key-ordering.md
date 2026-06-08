# Key Ordering

ZoneTree stores keys in comparer order. This is one of the most important design properties of the engine.

Ordered keys make it possible to build:

* range scans,
* prefix scans,
* secondary indexes,
* time-series layouts,
* ordered queues,
* leaderboard-style structures,
* partitioned keyspaces.

## Comparers Define The Keyspace

The comparer determines the order of keys. Two trees with different comparers are different ordered worlds, even if they store the same key type.

```csharp
using ZoneTree;
using ZoneTree.Comparers;

using var zoneTree = new ZoneTreeFactory<int, string>()
    .SetComparer(new Int32ComparerAscending())
    .SetDataDirectory("data/index")
    .OpenOrCreate();
```

Choose the comparer intentionally. It controls lookup, iteration, range scans, and disk segment ordering.

## Ordering Is Part Of The Database Shape

Do not change comparer semantics after a ZoneTree has been created.

ZoneTree stores the comparer type in metadata and validates it when opening an existing database. If the runtime comparer type does not match the metadata, opening the tree fails with a comparer mismatch error.

That check protects against accidentally opening a database with a different comparer class. It does not prove that a custom comparer with the same type still has the same behavior. Avoid changing the comparison logic, culture rules, constructor state, or field-ordering semantics of a comparer used by an existing database.

The reason is structural: mutable segments, read-only segments, disk segments, sparse arrays, merges, iterators, and point lookups all use the configured comparer and assume the stored data is already ordered by that same comparer.

If you need a different ordering, create a new ZoneTree with the new comparer or key encoding and copy/rebuild the data into it. If only the comparer class was renamed but its ordering is exactly the same, that is a metadata migration problem, not an ordering change.

You can evolve the way you encode future keys only when the new encoded keys still sort correctly under the existing comparer and do not break the keyspace layout your application already depends on.

## Key Layouts Matter

When building higher-level systems, encode your key so natural scans are cheap.

Examples:

```text
tenantId:userId
userId:createdAt:eventId
indexName:term:documentId
queueName:sequence
sensorId:timestamp
```

ZoneTree does not impose a schema. Your key layout is the schema.

## Forward And Reverse Order

ZoneTree supports both forward and reverse iterators. Reverse iteration is useful for latest-first time-series queries, descending leaderboards, and scanning the tail of ordered data.

See [iteration and range scans](../usage/iteration-and-range-scans.md).
