# Getting Started

This guide opens a persistent ZoneTree, writes a record, reads it back, and creates an iterator.

## Install

```bash
dotnet add package ZoneTree
```

## Open A Tree

```csharp
using ZoneTree;

using var zoneTree = new ZoneTreeFactory<int, string>()
    .SetDataDirectory("data/my-zone-tree")
    .OpenOrCreate();
```

`SetDataDirectory` controls where ZoneTree stores its metadata, WAL files, and disk segments unless more specific directories/providers are configured.

## Write And Read

```csharp
var opIndex = zoneTree.Upsert(1, "Hello ZoneTree");

if (zoneTree.TryGet(1, out var value))
{
    Console.WriteLine(value);
}
```

`Upsert` is the normal high-throughput write path. It adds or replaces a value and returns an operation index for the write.

## Iterate In Key Order

```csharp
using var iterator = zoneTree.CreateIterator();

iterator.Seek(1);

while (iterator.Next())
{
    Console.WriteLine($"{iterator.CurrentKey}: {iterator.CurrentValue}");
}
```

ZoneTree stores keys in comparer order. That makes range scans, prefix layouts, secondary indexes, time-series layouts, and ordered queues natural to build.

## Run Maintenance

ZoneTree uses an LSM-tree architecture. Writes first enter an in-memory mutable segment. Maintenance moves data forward and merges persistent segments.

```csharp
using var maintainer = zoneTree.CreateMaintainer();

zoneTree.Upsert(2, "value");

maintainer.WaitForBackgroundThreads();
```

For long-running applications, keep a maintainer alive while the tree is active.

## Next Steps

* Learn the [LSM tree](concepts/lsm-tree.md).
* Choose the right [read and write API](usage/reads-and-writes.md).
* Understand [value mutability](concepts/value-mutability.md).
* Understand [memory usage](storage/memory-usage.md).
* Pick a [WAL mode](durability/wal-modes.md).
