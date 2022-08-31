[![Downloads](https://img.shields.io/nuget/dt/ZoneTree?style=for-the-badge&labelColor=319e12&color=55c212)](https://www.nuget.org/packages/ZoneTree/) [![ZoneTree](https://img.shields.io/github/stars/koculu/ZoneTree?style=for-the-badge&logo=github&label=github&color=f1c400&labelColor=454545&logoColor=ffffff)](https://github.com/koculu/ZoneTree)

## Transaction Support
ZoneTree supports Optimistic Transactions. It is proud to announce that the ZoneTree is ACID-compliant. Of course, you can use non-transactional API for the scenarios where eventual consistency is sufficient.

ZoneTree supports 3 way of doing transactions.
1. Fluent Transactions with ready to use retry capability.
2. Classical Transaction API.
3. Exceptionless Transaction API.

The following sample shows how to do the transactions with ZoneTree Fluent Transaction API.

```c#
using var zoneTree = new ZoneTreeFactory<int, int>()
    // Additional stuff goes here
    .OpenOrCreateTransactional();
using var transaction =
    zoneTree
        .BeginFluentTransaction()
        .Do((tx) => zoneTree.UpsertNoThrow(tx, 3, 9))
        .Do((tx) =>
        {
            if (zoneTree.TryGetNoThrow(tx, 3, out var value).IsAborted)
                return TransactionResult.Aborted();
            if (zoneTree.UpsertNoThrow(tx, 3, 21).IsAborted)
                return TransactionResult.Aborted();
            return TransactionResult.Success();
        })
        .SetRetryCountForPendingTransactions(100)
        .SetRetryCountForAbortedTransactions(10);
    await transaction.CommitAsync();
```

The following sample shows traditional way of doing transactions with ZoneTree.
```c#
 using var zoneTree = new ZoneTreeFactory<int, int>()
    // Additional stuff goes here
    .OpenOrCreateTransactional();
 try 
 {
     var txId = zoneTree.BeginTransaction();
     zoneTree.TryGet(txId, 3, out var value);
     zoneTree.Upsert(txId, 3, 9);
     var result = zoneTree.Prepare(txId);
     while (result.IsPendingTransactions) {
         Thread.Sleep(100);
         result = zoneTree.Prepare(txId);
     }
     zoneTree.Commit(txId);
  }
  catch(TransactionAbortedException e)
  {
      //retry or cancel
  }
```
