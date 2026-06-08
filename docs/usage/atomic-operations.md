# Atomic Operations

Atomic methods are for same-key read-modify-write operations across LSM-tree segments.

They are useful for:

* counters,
* compare-and-set logic,
* appending to a value,
* updating an aggregate,
* initializing a value if absent and updating if present.

They are not the default write API. Use `Upsert` for simple inserts and replacements.

## Add Or Update

```csharp
zoneTree.TryAtomicAddOrUpdate(
    key: 42,
    valueToAdd: 1,
    valueUpdater: (ref int value) =>
    {
        value++;
        return true;
    },
    result: (in int value, long opIndex, OperationResult result) =>
    {
        Console.WriteLine($"{result}: {value} at {opIndex}");
    });
```

The updater decides whether the existing value should change. Returning `false` cancels the update. For `TryAtomicGetAndUpdate`, that means the key was found but no new value was written; for add-or-update APIs, cancellation is reported as `OperationResult.Cancelled`.

`ValueUpdaterDelegate<TValue>` receives a local value by `ref`, and it is designed to update that value. For mutable reference types, in-place mutation is valid when the delegate commits by returning `true`. If the delegate may cancel, decide first, then mutate or assign.

```csharp
zoneTree.TryAtomicGetAndUpdate(42, out var user, (ref User value) =>
{
    if (!ShouldRename(value))
        return false;

    value = value with { Name = "Bob" };
    return true;
});
```

See [value mutability](../concepts/value-mutability.md).

## Why Atomic Methods Exist

ZoneTree is an LSM-tree. A key can exist in several layers before compaction removes older versions. Atomic methods synchronize the read-decision-write sequence so other atomic methods observe a consistent ordering.

## Mixing Atomic And Regular Writes

You can use atomic methods for specific hot keys while the rest of the tree uses `Upsert`.

Example:

* `stats:user:123` uses atomic increment,
* `profile:user:123` uses normal `Upsert`,
* index entries use normal `Upsert` or `ForceDelete`.

Avoid mixing regular writes and atomic writes for the same invariant. If a value must be protected by atomic read-modify-write, keep all writes for that value on the atomic path.

## When To Use Transactions Instead

Atomic methods coordinate one key. Use transactions when the correctness rule spans multiple keys.
