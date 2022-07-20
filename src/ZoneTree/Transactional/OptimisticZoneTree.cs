using Tenray;
using ZoneTree.Collections;
using ZoneTree.Core;
using ZoneTree.Serializers;

namespace ZoneTree.Transactional;

public sealed class OptimisticZoneTree<TKey, TValue> : 
    ITransactionalZoneTree<TKey, TValue>,
    ITransactionalZoneTreeMaintenance<TKey, TValue>
{
    readonly ZoneTreeOptions<TKey, TValue> Options;

    readonly ITransactionLog<TKey, TValue> TransactionLog;

    readonly Dictionary<long, OptimisticTransaction<TKey, TValue>> OptimisticTransactions = new();

    public IZoneTree<TKey, TValue> ZoneTree { get; }

    public ITransactionalZoneTreeMaintenance<TKey, TValue> Maintenance => this;

    public IReadOnlyList<long> UncommittedTransactionIds => TransactionLog.UncommittedTransactionIds;

    public OptimisticZoneTree(
        ZoneTreeOptions<TKey, TValue> options,
        ITransactionLog<TKey, TValue> transactionLog,
        IZoneTree<TKey, TValue> zoneTree = null)
    {
        Options = options;
        TransactionLog = transactionLog;
        ZoneTree = zoneTree ?? new ZoneTree<TKey, TValue>(options);        
    }

    OptimisticTransaction<TKey, TValue> GetOrCreateTransaction(long transactionId)
    {
        var state = TransactionLog.GetTransactionState(transactionId);
        if (state == TransactionState.Aborted)
            throw new TransactionAbortedException(transactionId);

        if (state == TransactionState.Committed)
            throw new TransactionAlreadyCommittedException(transactionId);

        if (OptimisticTransactions.TryGetValue(transactionId, out var transaction))
            return transaction;

        transaction = new OptimisticTransaction<TKey, TValue>(transactionId, Options, TransactionLog);
        OptimisticTransactions.Add(transactionId, transaction);
        return transaction;
    }

    /// <summary>
    /// Deletes the transaction instance from memory and disposes the resources.
    /// Transaction Manager holds the permanenet status of the transaction.
    /// </summary>
    /// <param name="transaction"></param>
    void DeleteTransaction(OptimisticTransaction<TKey, TValue> transaction)
    {
        OptimisticTransactions.Remove(transaction.TransactionId);
    }

    public long BeginTransaction()
    {
        var transactionId = TransactionLog.GetNextTransactionId();
        TransactionLog.TransactionStarted(transactionId);
        return transactionId;
    }

    public void Rollback(long transactionId)
    {
        lock (this)
        {
            var transaction = GetOrCreateTransaction(transactionId);
            AbortTransaction(transaction);
        }
    }

    void AbortTransaction(OptimisticTransaction<TKey, TValue> transaction)
    {
        var transactionId = transaction.TransactionId;
        /*
         For each oldOj, oldWTS(Oj) in OLD(Ti)
            if WTS(Oj) equals TS(Ti) then restore Oj = oldOj and WTS(Oj) = oldWTS(Oj)
         */
        var history = TransactionLog.GetHistory(transactionId);
        foreach (var item in history)
        {
            var key = item.Key;
            var oldValue = item.Value.Value1;
            var oldWriteStamp = item.Value.Value2;
            TransactionLog.TryGetReadWriteStamp(key, out var readWriteStamp);
            if (readWriteStamp.WriteStamp != transactionId)
                continue;
            readWriteStamp.WriteStamp = oldWriteStamp;
            TransactionLog.AddOrUpdateReadWriteStamp(key, readWriteStamp);
            ZoneTree.Upsert(key, oldValue);
        }
        DeleteTransaction(transaction);
        TransactionLog.TransactionAborted(transactionId);
    }

    public CommitResult Prepare(long transactionId)
    {
        lock (this)
        {
            var transaction = GetOrCreateTransaction(transactionId);
            return DoPrepare(transaction);
        }
    }

    public CommitResult PrepareAndCommit(long transactionId)
    {
        lock (this)
        {
            var transaction = GetOrCreateTransaction(transactionId);
            var result = DoPrepare(transaction);
            if (result.IsReadyToCommit)
                return DoCommit(transaction);
            return result;
        }
    }

    public CommitResult Commit(long transactionId)
    {
        lock (this)
        {
            var transaction = GetOrCreateTransaction(transactionId);
            return DoCommit(transaction);
        }
    }

    CommitResult DoCommit(OptimisticTransaction<TKey, TValue> transaction)
    {
        if (!transaction.IsReadyToCommit)
            throw new TransactionIsNotReadyToCommitException(transaction.TransactionId);
        DeleteTransaction(transaction);
        TransactionLog.TransactionCommitted(transaction.TransactionId);
        return CommitResult.Committed;
    }

    CommitResult DoPrepare(OptimisticTransaction<TKey, TValue> transaction)
    {
        var transactionId = transaction.TransactionId;
        var dependencies = TransactionLog.GetDependencyList(transactionId);
        var waitList = new List<long>();
        foreach (var dependency in dependencies)
        {
            var state = TransactionLog.GetTransactionState(dependency);
            if (state == TransactionState.Aborted)
            {
                // If there is a transaction in DEP(Ti) that aborted then abort
                AbortTransaction(transaction);
                throw new TransactionAbortedException(transactionId);
            }
            if (state == TransactionState.Uncommitted)
                waitList.Add(dependency);
        }

        if (waitList.Count == 0)
        {
            transaction.IsReadyToCommit = true;
            return CommitResult.ReadyToCommit;
        }

        // While there is a transaction DEP(Ti) that has not ended: wait
        return new CommitResult(
            CommitState.PendingTransactions,
            waitList);
    }

    public bool ContainsKey(long transactionId, in TKey key)
    {
        lock (this)
        {
            var transaction = GetOrCreateTransaction(transactionId);
            var hasReadWriteStamp = TransactionLog.TryGetReadWriteStamp(key, out var readWriteStamp);
            var hasKey = ZoneTree.ContainsKey(in key);
            if (transaction.HandleReadKey(ref readWriteStamp) == OptimisticReadAction.Abort)
            {
                AbortTransaction(transaction);
                throw new TransactionAbortedException(transactionId);
            }
            TransactionLog.AddOrUpdateReadWriteStamp(key, in readWriteStamp);
            return hasKey;
        }
    }

    public bool TryGet(long transactionId, in TKey key, out TValue value)
    {
        lock (this)
        {
            var transaction = GetOrCreateTransaction(transactionId);
            var hasReadWriteStamp = TransactionLog.TryGetReadWriteStamp(key, out var readWriteStamp);
            var hasValue = ZoneTree.TryGet(in key, out value);
            if (transaction.HandleReadKey(ref readWriteStamp) == OptimisticReadAction.Abort)
            {
                AbortTransaction(transaction);
                throw new TransactionAbortedException(transactionId);
            }

            TransactionLog.AddOrUpdateReadWriteStamp(key, in readWriteStamp);
            return hasValue;
        }
    }

    public bool Upsert(long transactionId, in TKey key, in TValue value)
    {
        lock (this)
        {
            var transaction = GetOrCreateTransaction(transactionId);
            var hasReadWriteStamp = TransactionLog.TryGetReadWriteStamp(key, out var readWriteStamp);
            var hasOldValue = ZoneTree.TryGet(in key, out var oldValue);

            var action = transaction.HandleWriteKey(
                ref readWriteStamp,
                in key,
                hasOldValue,
                in oldValue);

            // skip the write based on Thomas Write Rule.
            if (action == OptimisticWriteAction.SkipWrite)
                return !hasOldValue;

            // abort case
            if (action == OptimisticWriteAction.Abort)
            {
                AbortTransaction(transaction);
                throw new TransactionAbortedException(transactionId);
            }

            // actual write happens.
            TransactionLog.AddOrUpdateReadWriteStamp(key, in readWriteStamp);
            ZoneTree.Upsert(in key, in value);
            return !hasOldValue;
        }
    }

    public void Delete(long transactionId, in TKey key)
    {
        lock (this)
        {
            var transaction = GetOrCreateTransaction(transactionId);
            var hasReadWriteStamp = TransactionLog.TryGetReadWriteStamp(key, out var readWriteStamp);
            var hasOldValue = ZoneTree.TryGet(in key, out var oldValue);

            var action = transaction.HandleWriteKey(
               ref readWriteStamp,
               in key,
               hasOldValue,
               in oldValue);

            // skip the write based on Thomas Write Rule.
            if (action == OptimisticWriteAction.SkipWrite)
                return;

            // abort case
            if (action == OptimisticWriteAction.Abort)
            {
                AbortTransaction(transaction);
                throw new TransactionAbortedException(transactionId);
            }
            TransactionLog.AddOrUpdateReadWriteStamp(key, in readWriteStamp);
            ZoneTree.ForceDelete(in key);
        }
    }

    public void DestroyTree()
    {
        TransactionLog.Dispose();
        ZoneTree.Maintenance.DestroyTree();
    }

    public void SaveMetaData()
    {
        ZoneTree.Maintenance.SaveMetaData();
    }

    public void Dispose()
    {
        TransactionLog.Dispose();
        ZoneTree.Dispose();
    }

    public void RollbackUncommittedTransactions()
    {
        lock (this)
        {
            var uncommitted = TransactionLog.UncommittedTransactionIds;
            foreach (var u in uncommitted)
            {
                var tx = GetOrCreateTransaction(u);
                AbortTransaction(tx);
            }
        }
    }

    public bool ReadCommittedContainsKey(in TKey key)
    {
        lock (this)
        {
            TransactionLog.TryGetReadWriteStamp(key, out var readWriteStamp);
            var ws = readWriteStamp.WriteStamp;
            if (TransactionLog.GetTransactionState(ws) == TransactionState.Committed)
            {
                // committed write stamp found.
                // Hence, the tree has committed data.
                var isFoundInTree = ZoneTree.ContainsKey(key);
                return isFoundInTree;
            }

            // Uncommitted write stamp found.
            // Search the history of the uncommitted transaction in a loop
            // to find the last committed history record.
            while (TransactionLog.GetHistory(ws).TryGetValue(key, out var history))
            {
                ws = history.Value2;
                if (TransactionLog.GetTransactionState(ws) == TransactionState.Uncommitted)
                    continue;
                // if history.writestamp == 0 then key did not exist before the write.
                if (ws == 0 || Options.IsValueDeleted(history.Value1))
                {
                    return false;
                }
                return true;
            }
            // cannot find record in history,
            // this means the key does not exist.
            return false;
        }
    }

    public bool ReadCommittedTryGet(in TKey key, out TValue value)
    {
        lock (this)
        {
            TransactionLog.TryGetReadWriteStamp(key, out var readWriteStamp);
            var ws = readWriteStamp.WriteStamp;            
            if (TransactionLog.GetTransactionState(ws) == TransactionState.Committed)
            {
                // committed write stamp found.
                // Hence, the tree has committed data.
                var isFoundInTree = ZoneTree.TryGet(key, out value);
                return isFoundInTree;
            }

            // Uncommitted write stamp found.
            // Search the history of the uncommitted transaction in a loop
            // to find the last committed history record.
            while (TransactionLog.GetHistory(ws).TryGetValue(key, out var history))
            {
                ws = history.Value2;
                if (TransactionLog.GetTransactionState(ws) == TransactionState.Uncommitted)
                    continue;
                // if history.writestamp == 0 then key did not exist before the write.
                if (ws == 0 || Options.IsValueDeleted(history.Value1))
                {
                    value = default;
                    return false;
                }
                value = history.Value1;
                return true;
            }
            // cannot find record in history,
            // this means the key does not exist.
            value = default;
            return false;
        }
    }

    public void UpsertAutoCommit(in TKey key, in TValue value)
    {
        lock (this)
        {
            var transactionId = BeginTransaction();
            var transaction = GetOrCreateTransaction(transactionId);
            TransactionLog.TryGetReadWriteStamp(key, out var readWriteStamp);
            readWriteStamp.WriteStamp = transactionId;
            TransactionLog.AddOrUpdateReadWriteStamp(key, in readWriteStamp);
            ZoneTree.Upsert(in key, in value);
            transaction.IsReadyToCommit = true;
            DoCommit(transaction);
        }
    }

    public void DeleteAutoCommit(in TKey key)
    {
        lock (this)
        {
            var transactionId = BeginTransaction();
            var transaction = GetOrCreateTransaction(transactionId);
            TransactionLog.TryGetReadWriteStamp(key, out var readWriteStamp);
            readWriteStamp.WriteStamp = transactionId;
            TransactionLog.AddOrUpdateReadWriteStamp(key, in readWriteStamp);
            ZoneTree.ForceDelete(in key);
            transaction.IsReadyToCommit = true;
            DoCommit(transaction);
        }
    }
}
