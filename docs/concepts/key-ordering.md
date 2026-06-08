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
