# Queues

ZoneTree can power persistent queue-like structures when keys encode ordering.

## Basic Layout

Use a monotonically increasing sequence as part of the key:

```text
queueName:sequence -> payload
```

Consumers can seek to the next sequence and iterate forward.

## Acknowledgement

Acknowledgement can be modeled with:

* deletion markers for consumed messages,
* separate offset records,
* consumer-group state keys,
* periodic compaction.

## Counters And Sequences

Atomic operations are useful for queue sequence counters.

```text
counter:queueName -> nextSequence
```

Use atomic read-modify-write for the counter key while queue payload records use normal `Upsert`.

```csharp
long sequence = 0;

sequenceTree.TryAtomicAddOrUpdate(
    "counter:orders",
    valueToAdd: 1L,
    valueUpdater: (ref long value) =>
    {
        value++;
        return true;
    },
    result: (in long value, long opIndex, OperationResult result) =>
    {
        sequence = value;
    });

var key = $"orders:{sequence:D20}";
payloadTree.Upsert(key, payload);
```

Consumers can seek to the last acknowledged sequence and iterate forward:

```csharp
using var iterator = payloadTree.CreateIterator();

iterator.Seek($"orders:{nextSequence:D20}");

while (iterator.Next())
{
    if (!iterator.CurrentKey.StartsWith("orders:", StringComparison.Ordinal))
        break;

    Process(iterator.CurrentValue);
}
```

## Retention

Use deletion markers or TTL-style values for retention. Maintenance and compaction remove obsolete records later.

## Scope

ZoneTree gives you ordered durable storage. Delivery guarantees, leases, visibility timeouts, retry policy, and distributed coordination belong to the queue layer you build above it.
