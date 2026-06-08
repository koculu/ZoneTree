# Value Mutability

Treat values stored in ZoneTree as immutable snapshots.

This rule is especially important when `TValue` is a mutable reference type. ZoneTree may hold the object reference while the value is still in an in-memory segment. If application code mutates that object after inserting it, the visible value can change without a new `Upsert`, without a WAL record, without a new operation index, and without predictable recovery behavior.

## The Problem

Avoid this pattern:

```csharp
var user = new User { Name = "Alice" };

zoneTree.Upsert(1, user);

user.Name = "Bob"; // wrong: mutates an object that may be stored by reference
```

Also avoid treating a value returned by `TryGet` as a live editable database record:

```csharp
if (zoneTree.TryGet(1, out var user))
{
    user.Name = "Bob"; // wrong: does not create a real ZoneTree write
}
```

Depending on whether the value came from the mutable segment, a read-only segment, or disk deserialization, this can behave differently. That makes the bug subtle and hard to reproduce.

## The Rule

To update a value, create a new value and write it back with `Upsert`, an atomic method, or a transaction.

```csharp
public sealed record User(string Name);

if (zoneTree.TryGet(1, out var user))
{
    var updated = user with { Name = "Bob" };
    zoneTree.Upsert(1, updated);
}
```

For mutable classes, clone first:

```csharp
if (zoneTree.TryGet(1, out var user))
{
    var updated = user.Clone();
    updated.Name = "Bob";
    zoneTree.Upsert(1, updated);
}
```

## Prefer Immutable Value Shapes

Small record-like values are often best represented as structs or readonly record structs.

```csharp
public readonly record struct CounterValue(long Count);

public readonly record struct UserScore(int Score, long UpdatedAt);

public readonly record struct QueuePointer(long Sequence);
```

This gives you:

* value semantics,
* less accidental shared mutation,
* lower GC pressure for in-memory segments,
* predictable atomic updates,
* compact serialized forms when paired with good serializers.

## When Reference Types Are Fine

Reference types are still useful for large or complex values. Use immutable classes, records, serialized payloads, or clone-and-upsert patterns.

Avoid storing mutable object graphs and then changing them in place.

## Atomic Updater Delegates

Atomic update delegates receive a local `TValue` variable by `ref`. They are designed to update that value.

For value types, this is straightforward. For mutable reference types, in-place mutation is valid when the delegate commits by returning `true`.

The caveat is cancellation. Returning `false` tells ZoneTree not to write the local value back. If the delegate mutates a shared reference object first and then returns `false`, that object may already have changed in memory.

Avoid mutating before the commit decision:

```csharp
zoneTree.TryAtomicGetAndUpdate(1, out var user, (ref User value) =>
{
    value.Name = "Bob";
    return ShouldCommit(value); // wrong: the object was already mutated
});
```

Prefer:

```csharp
zoneTree.TryAtomicGetAndUpdate(1, out var user, (ref User value) =>
{
    if (!ShouldRename(value))
        return false;

    value = value with { Name = "Bob" };
    return true;
});
```

If the update will commit, in-place mutation is fine:

```csharp
zoneTree.TryAtomicGetAndUpdate(1, out var user, (ref User value) =>
{
    if (!ShouldRename(value))
        return false;

    value.Name = "Bob";
    return true;
});
```

You can also assign a replacement value when that better matches your value model.

Decide first, then mutate or assign. Returning `false` should mean the delegate made no observable change.
