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

Use `Seek` to move near a key and continue from there.

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

Reverse iteration is useful for latest-first time-series queries, descending indexes, and scanning high-key ranges.

## Iterator Types

ZoneTree iterators can operate with different refresh behavior.

Use the default iterator for ordinary scans. Use snapshot-style iterators when you need a consistent read-only view that ignores later writes.

## Deleted Records

By default, iterators return live records. Some APIs allow including deleted records when you need low-level inspection, diagnostics, or custom compaction workflows.

## Dispose Iterators

Iterators can hold references to active segments. Dispose them when you are done.

```csharp
using var iterator = zoneTree.CreateIterator();
```

Long-lived iterators can delay segment disposal, so keep them scoped to the scan.
