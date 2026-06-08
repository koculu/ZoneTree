# Iteration And Range Scans

ZoneTree stores keys in order. Iterators expose that order for full scans, range scans, prefix layouts, and reverse reads.

## Forward Iteration

```csharp
using var iterator = zoneTree.CreateIterator();

while (iterator.Next())
{
    Console.WriteLine($"{iterator.CurrentKey}: {iterator.CurrentValue}");
}
```

## Seek

Use `Seek` to move near a key and continue from there. A forward iterator seeks to the first key greater than or equal to the target key.

```csharp
using var iterator = zoneTree.CreateIterator();

iterator.Seek(100);

while (iterator.Next())
{
    if (iterator.CurrentKey >= 200)
        break;

    Console.WriteLine(iterator.CurrentValue);
}
```

## Reverse Iteration

```csharp
using var iterator = zoneTree.CreateReverseIterator();

iterator.Seek(1000);

while (iterator.Next())
{
    Console.WriteLine($"{iterator.CurrentKey}: {iterator.CurrentValue}");
}
```

Reverse iteration is useful for latest-first time-series queries, descending indexes, and scanning high-key ranges. A reverse iterator seeks to the last key smaller than or equal to the target key.

## Iterator Types

ZoneTree iterators can operate with different refresh behavior.

| Type | Behavior |
| --- | --- |
| `IteratorType.AutoRefresh` | Default. Scans all available segments and refreshes when the mutable segment moves forward. New writes may appear if their position has not already been passed. |
| `IteratorType.NoRefresh` | Scans all available segments captured by the iterator and does not automatically include newly moved segments. It can be manually refreshed. |
| `IteratorType.Snapshot` | Moves the mutable segment forward when the iterator is created, then scans read-only, disk, and bottom segments. It does not see later writes. |
| `IteratorType.ReadOnlyRegion` | Like snapshot, but does not move the mutable segment forward. Manually move the mutable segment first if you need current in-memory writes included. |

Use the default iterator for ordinary scans. Use `Snapshot` when you need a stable view and can accept the cost of moving the mutable segment forward.

```csharp
using var iterator = zoneTree.CreateIterator(IteratorType.Snapshot);
```

## Deleted Records

By default, iterators return live records. Pass `includeDeletedRecords: true` when you need low-level inspection, diagnostics, or custom compaction workflows.

```csharp
using var iterator = zoneTree.CreateIterator(
    IteratorType.NoRefresh,
    includeDeletedRecords: true);
```

Normal application scans should usually leave deleted records hidden.

## Block Cache Contribution

Disk segment iterator reads do not contribute to the block cache by default. Enable cache contribution when the scan represents a useful working set that will probably be read again.

```csharp
using var iterator = zoneTree.CreateIterator(
    IteratorType.AutoRefresh,
    includeDeletedRecords: false,
    contributeToTheBlockCache: true);
```

For one-off full scans, keep cache contribution disabled so the scan does not evict hotter random-read data.

## Low-Level Segment Iterators

The implementation also exposes low-level in-memory and read-only-segment iterators. These are useful for diagnostics and internal tooling, not normal application reads.

## Dispose Iterators

Iterators can hold references to active segments. Dispose them when you are done.

```csharp
using var iterator = zoneTree.CreateIterator();
```

Long-lived iterators can delay segment disposal, so keep them scoped to the scan.
