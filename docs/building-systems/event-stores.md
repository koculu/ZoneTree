# Event Stores

Event stores fit naturally on ordered key-value storage. The key usually encodes stream and version.

## Stream Layout

```text
streamId:version -> event
```

This supports fast stream reads by seeking to the first version and iterating forward.

```csharp
var streamPrefix = "stream:order-123:";

using var iterator = eventTree.CreateIterator();
iterator.Seek(streamPrefix);

while (iterator.Next())
{
    if (!iterator.CurrentKey.StartsWith(streamPrefix, StringComparison.Ordinal))
        break;

    Apply(iterator.CurrentValue);
}
```

Use fixed-width version encoding so lexical order matches numeric order:

```text
stream:order-123:00000000000000000001
stream:order-123:00000000000000000002
```

## Global Order

If you need a global event order, maintain a separate index:

```text
globalSequence -> streamId:version
```

Atomic operations can allocate sequence numbers. Transactions can coordinate stream and global index writes when required.

```csharp
using var eventTree = new ZoneTreeFactory<string, string>()
    .SetDataDirectory("data/events")
    .OpenOrCreateTransactional();

var tx = eventTree.BeginTransaction();

eventTree.Upsert(tx, "stream:order-123:00000000000000000042", eventJson);
eventTree.Upsert(tx, "global:00000000000000102490", "order-123:42");

var result = eventTree.PrepareAndCommit(tx);
```

## Idempotency

Use deterministic event IDs or idempotency keys to protect append operations from retries.

```text
idempotency:requestId -> streamId:version
```

## Snapshots

Snapshots can be stored in another tree or key range:

```text
snapshot:streamId:version -> snapshot
```

## Retention

Event retention is a product decision. ZoneTree supports deletion markers and compaction, but your event-store layer defines what can be removed.
