# Transactions

Transactions coordinate changes across multiple keys. Use them when a workflow must read or write several keys as one logical unit.

For simple high-throughput writes, prefer the non-transactional API.

## Open A Transactional Tree

```csharp
using ZoneTree;

using var zoneTree = new ZoneTreeFactory<int, string>()
    .SetDataDirectory("data/tx")
    .OpenOrCreateTransactional();
```

## Basic Transaction

```csharp
var tx = zoneTree.BeginTransaction();

zoneTree.Upsert(tx, 1, "first");
zoneTree.Upsert(tx, 2, "second");

var result = zoneTree.PrepareAndCommit(tx);

Console.WriteLine(result);
```

## When To Use Transactions

Use transactions for:

* moving value from one key to another,
* updating a primary record and secondary index together,
* ensuring multiple keys change together,
* checking multiple keys before writing.

Do not use transactions merely because writes are concurrent. Regular writes are designed for concurrent workloads.

## Atomic Methods Vs Transactions

| Requirement | Use |
| --- | --- |
| Current value of one key decides the next value | atomic method |
| Multiple keys must commit together | transaction |
| Simple replace/add/delete | regular write API |

## Keep Transactions Short

Keep transaction work focused. Long-running transactions increase conflict windows and can make other transactions wait or retry.
