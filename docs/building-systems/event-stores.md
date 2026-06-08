# Event Stores

Event stores fit naturally on ordered key-value storage. The key usually encodes stream and version.

## Stream Layout

```text
streamId:version -> event
```

This supports fast stream reads by seeking to the first version and iterating forward.

## Global Order

If you need a global event order, maintain a separate index:

```text
globalSequence -> streamId:version
```

Atomic operations can allocate sequence numbers. Transactions can coordinate stream and global index writes when required.

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
