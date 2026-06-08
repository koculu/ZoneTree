# Deletion Markers And TTL

ZoneTree uses deletion markers. A delete writes a new record that represents deletion, and compaction removes obsolete data later.

This is the normal model for LSM-tree storage engines. It lets deletes be fast writes instead of immediate expensive rewrites of older segments.

## Delete APIs

Use `TryDelete` when you want to delete only if the key currently exists.

```csharp
zoneTree.TryDelete(42, out var opIndex);
```

Use `ForceDelete` when you want to write a deletion marker without checking older layers first.

```csharp
zoneTree.ForceDelete(42);
```

`ForceDelete` is faster, but it may create a deletion marker even when no older record exists.

## Custom Deletion Logic

For many primitive and nullable types, ZoneTree can provide default deletion behavior. You can also define your own marker.

```csharp
using var zoneTree = new ZoneTreeFactory<int, int>()
    .SetIsDeletedDelegate((in int key, in int value) => value == -1)
    .SetMarkValueDeletedDelegate((ref int value) => value = -1)
    .OpenOrCreate();
```

## TTL

TTL can be modeled with the same mechanism. Store an expiration timestamp in the value and make the delete predicate return `true` after expiration.

```csharp
public readonly record struct CacheEntry(string Value, DateTime ExpiresAt);

using var zoneTree = new ZoneTreeFactory<string, CacheEntry>()
    .SetIsDeletedDelegate((in string key, in CacheEntry value) =>
        value.ExpiresAt <= DateTime.UtcNow)
    .SetMarkValueDeletedDelegate((ref CacheEntry value) =>
        value = value with { ExpiresAt = DateTime.MinValue })
    .OpenOrCreate();
```

Expired records are treated as deleted records. Maintenance and compaction can remove obsolete data later.

## Disable Deletion

If your database does not need deletions and you want to store default values normally, disable deletion:

```csharp
using var zoneTree = new ZoneTreeFactory<int, int>()
    .DisableDeletion()
    .OpenOrCreate();
```
