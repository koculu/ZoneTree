using System.Diagnostics;
using Tenray.ZoneTree.Exceptions;
using Tenray.ZoneTree.Core;
using Tenray.ZoneTree.Serializers;
using Tenray.ZoneTree.Options;
using Tenray.ZoneTree.Logger;

namespace Tenray.ZoneTree.Transactional;

public sealed class OptimisticZoneTree<TKey, TValue> :
    ITransactionalZoneTree<TKey, TValue>,
    ITransactionalZoneTreeMaintenance<TKey, TValue>
{
    readonly ZoneTreeOptions<TKey, TValue> Options;

    readonly Dictionary<long, OptimisticTransaction<TKey, TValue>> OptimisticTransactions = new();

    public IZoneTree<TKey, TValue> ZoneTree { get; }

    public ITransactionLog<TKey, TValue> TransactionLog { get; }

    public ITransactionalZoneTreeMaintenance<TKey, TValue> Maintenance => this;

    public IReadOnlyList<long> UncommittedTransactionIds => TransactionLog.UncommittedTransactionIds;

    public bool IsReadOnly
    {
        get => ZoneTree.IsReadOnly;
        set => ZoneTree.IsReadOnly = value;
    }

    public ILogger Logger => ZoneTree.Logger;

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
        var transaction = GetOrCreateTransactionNoAbortThrow(transactionId);
        if (transaction == null)
            throw new TransactionAbortedException(transactionId);
        return transaction;
    }

    OptimisticTransaction<TKey, TValue> GetOrCreateTransactionNoAbortThrow(long transactionId)
    {
        var state = TransactionLog.GetTransactionState(transactionId);
        if (state == TransactionState.Aborted)
            return null;

        if (state == TransactionState.Committed)
            throw new TransactionAlreadyCommittedException(transactionId);

        if (OptimisticTransactions.TryGetValue(transactionId, out var transaction))
            return transaction;

        transaction = new OptimisticTransaction<TKey, TValue>(transactionId, Options, TransactionLog);
        OptimisticTransactions.Add(transactionId, transaction);
        return transaction;
    }

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

    public FluentTransaction<TKey, TValue> BeginFluentTransaction()
    {
        return new FluentTransaction<TKey, TValue>(this);
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
        var result = PrepareNoThrow(transactionId);
        if (result.IsAborted)
            throw new TransactionAbortedException(transactionId);
        return result;
    }

    public TransactionState GetTransactionState(long transactionId)
    {
        return TransactionLog.GetTransactionState(transactionId);
    }

    public CommitResult PrepareNoThrow(long transactionId)
    {
        lock (this)
        {
            var transaction = GetOrCreateTransactionNoAbortThrow(transactionId);
            if (transaction == null)
                return CommitResult.Aborted;
            return DoPrepare(transaction);
        }
    }

    public CommitResult PrepareAndCommit(long transactionId)
    {
        lock (this)
        {
            var result = PrepareAndCommitNoThrow(transactionId);
            if (result.IsAborted)
                throw new TransactionAbortedException(transactionId);
            return result;
        }
    }

    public CommitResult PrepareAndCommitNoThrow(long transactionId)
    {
        lock (this)
        {
            var transaction = GetOrCreateTransactionNoAbortThrow(transactionId);
            if (transaction == null)
                return CommitResult.Aborted;
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

    public CommitResult CommitNoThrow(long transactionId)
    {
        lock (this)
        {
            var transaction = GetOrCreateTransactionNoAbortThrow(transactionId);
            if (transaction == null)
                return CommitResult.Aborted;
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
                return CommitResult.Aborted;
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
        var result = ContainsKeyNoThrow(transactionId, in key);
        if (result.IsAborted)
            throw new TransactionAbortedException(transactionId);
        return result.Result;
    }

    public bool TryGet(long transactionId, in TKey key, out TValue value)
    {
        var result = TryGetNoThrow(transactionId, in key, out value);
        if (result.IsAborted)
            throw new TransactionAbortedException(transactionId);
        return result.Result;
    }

    public bool Upsert(long transactionId, in TKey key, in TValue value)
    {
        var result = UpsertNoThrow(transactionId, in key, in value);
        if (result.IsAborted)
            throw new TransactionAbortedException(transactionId);
        return result.Result;
    }

    public void Delete(long transactionId, in TKey key)
    {
        var result = DeleteNoThrow(transactionId, in key);
        if (result.IsAborted)
            throw new TransactionAbortedException(transactionId);
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

    public bool ReadCommittedContainsKey(in TKey key, long currentTransactionId = -1)
    {
        lock (this)
        {
            TransactionLog.TryGetReadWriteStamp(key, out var readWriteStamp);
            var ws = readWriteStamp.WriteStamp;
            if (ws == currentTransactionId)
            {
                // given transaction matches write-stamp.
                // we can read uncommitted for current transaction.
                var isFoundInTree = ZoneTree.ContainsKey(key);
                return isFoundInTree;
            }
            var state = TransactionLog.GetTransactionState(ws);
            if (state == TransactionState.Committed)
            {
                // committed write stamp found.
                // Hence, the tree has committed data.
                var isFoundInTree = ZoneTree.ContainsKey(key);
                return isFoundInTree;
            }

            // At this stage, the write stamp cannot be an aborted transaction,
            // because it is already rollbacked.
            Debug.Assert(state != TransactionState.Aborted);

            // Uncommitted write stamp found.
            // Search the history of the uncommitted transaction in a loop
            // to find the last committed history record.
            while (TransactionLog.GetHistory(ws).TryGetValue(key, out var history))
            {
                ws = history.Value2;
                state = TransactionLog.GetTransactionState(ws);
                if (state == TransactionState.Uncommitted)
                    continue;

                Debug.Assert(state != TransactionState.Aborted);

                if (Options.IsValueDeleted(history.Value1))
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

    public bool ReadCommittedTryGet(in TKey key, out TValue value, long currentTransactionId = -1)
    {
        lock (this)
        {
            TransactionLog.TryGetReadWriteStamp(key, out var readWriteStamp);
            var ws = readWriteStamp.WriteStamp;
            if (ws == currentTransactionId)
            {
                // given transaction matches write-stamp.
                // we can read uncommitted for current transaction.
                var isFoundInTree = ZoneTree.TryGet(key, out value);
                return isFoundInTree;
            }

            var state = TransactionLog.GetTransactionState(ws);
            if (state == TransactionState.Committed)
            {
                // committed write stamp found.
                // Hence, the tree has committed data.
                var isFoundInTree = ZoneTree.TryGet(key, out value);
                return isFoundInTree;
            }

            // At this stage, the write stamp cannot be an aborted transaction,
            // because it is already rollbacked.
            Debug.Assert(state != TransactionState.Aborted);

            // Uncommitted write stamp found.
            // Search the history of the uncommitted transaction in a loop
            // to find the last committed history record.
            while (TransactionLog.GetHistory(ws).TryGetValue(key, out var history))
            {
                ws = history.Value2;
                state = TransactionLog.GetTransactionState(ws);
                if (state == TransactionState.Uncommitted)
                    continue;

                Debug.Assert(state != TransactionState.Aborted);

                if (Options.IsValueDeleted(history.Value1))
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

    public void UpsertAutoCommit(in TKey key, in TValue oldVal)
    {
        if (IsReadOnly)
            throw new ZoneTreeIsReadOnlyException();
        lock (this)
        {
            var transactionId = BeginTransaction();
            var transaction = GetOrCreateTransaction(transactionId);
            TransactionLog.TryGetReadWriteStamp(key, out var readWriteStamp);
            readWriteStamp.WriteStamp = transactionId;
            if (Options.WriteAheadLogOptions.EnableIncrementalBackup)
            {
                // if incremental backup is enabled,
                // add the history record.
                if (!ZoneTree.TryGet(in key, out var oldValue))
                    Options.MarkValueDeleted(ref oldValue);
                var combinedValue =
                    new CombinedValue<TValue, long>(oldValue, readWriteStamp.WriteStamp);
                TransactionLog.AddHistoryRecord(transactionId, key, combinedValue);
            }
            TransactionLog.AddOrUpdateReadWriteStamp(key, in readWriteStamp);
            ZoneTree.Upsert(in key, in oldVal);
            transaction.IsReadyToCommit = true;
            DoCommit(transaction);
        }
    }

    public void DeleteAutoCommit(in TKey key)
    {
        if (IsReadOnly)
            throw new ZoneTreeIsReadOnlyException();
        lock (this)
        {
            var transactionId = BeginTransaction();
            var transaction = GetOrCreateTransaction(transactionId);
            TransactionLog.TryGetReadWriteStamp(key, out var readWriteStamp);
            readWriteStamp.WriteStamp = transactionId;
            if (Options.WriteAheadLogOptions.EnableIncrementalBackup)
            {
                // if incremental backup is enabled,
                // add the history record.
                if (!ZoneTree.TryGet(in key, out var oldValue))
                    Options.MarkValueDeleted(ref oldValue);
                var combinedValue =
                    new CombinedValue<TValue, long>(oldValue, readWriteStamp.WriteStamp);
                TransactionLog.AddHistoryRecord(transactionId, key, combinedValue);
            }
            TransactionLog.AddOrUpdateReadWriteStamp(key, in readWriteStamp);
            ZoneTree.ForceDelete(in key);
            transaction.IsReadyToCommit = true;
            DoCommit(transaction);
        }
    }

    public int RollbackAllUncommitted()
    {
        var count = 0;
        var uncommitted = TransactionLog.UncommittedTransactionIds;        
        foreach (var u in uncommitted)
        {
            Rollback(u);
            ++count;
        }
        return count;
    }

    public int RollbackUncommittedTransactionIdsBefore(DateTime dateTime)
    {
        var count = 0;
        var uncommitted = TransactionLog.GetUncommittedTransactionIdsBefore(dateTime);
        foreach (var u in uncommitted)
        {
            Rollback(u);
            ++count;
        }
        return count;
    }

    public TransactionResult<bool> ContainsKeyNoThrow(long transactionId, in TKey key)
    {
        lock (this)
        {
            var transaction = GetOrCreateTransactionNoAbortThrow(transactionId);
            if (transaction == null)
                return TransactionResult<bool>.Aborted();
            var hasReadWriteStamp = TransactionLog.TryGetReadWriteStamp(key, out var readWriteStamp);
            var hasKey = ZoneTree.ContainsKey(in key);
            if (transaction.HandleReadKey(ref readWriteStamp) == OptimisticReadAction.Abort)
            {
                AbortTransaction(transaction);
                return TransactionResult<bool>.Aborted();
            }
            TransactionLog.AddOrUpdateReadWriteStamp(key, in readWriteStamp);
            return TransactionResult<bool>.Success(hasKey);
        }
    }

    public TransactionResult<bool> TryGetNoThrow(long transactionId, in TKey key, out TValue value)
    {
        lock (this)
        {
            var transaction = GetOrCreateTransactionNoAbortThrow(transactionId);
            if (transaction == null)
            {
                value = default;
                return TransactionResult<bool>.Aborted();
            }
            var hasReadWriteStamp = TransactionLog.TryGetReadWriteStamp(key, out var readWriteStamp);
            var hasValue = ZoneTree.TryGet(in key, out value);
            if (transaction.HandleReadKey(ref readWriteStamp) == OptimisticReadAction.Abort)
            {
                AbortTransaction(transaction);
                return TransactionResult<bool>.Aborted();
            }

            TransactionLog.AddOrUpdateReadWriteStamp(key, in readWriteStamp);
            return TransactionResult<bool>.Success(hasValue);
        }
    }


    public TransactionResult<bool> UpsertNoThrow(long transactionId, in TKey key, in TValue value)
    {
        if (IsReadOnly)
            throw new ZoneTreeIsReadOnlyException();
        lock (this)
        {
            var transaction = GetOrCreateTransactionNoAbortThrow(transactionId);
            if (transaction == null)
                return TransactionResult<bool>.Aborted();

            var hasReadWriteStamp = TransactionLog.TryGetReadWriteStamp(key, out var readWriteStamp);
            var hasOldValue = ZoneTree.TryGet(in key, out var oldValue);

            var action = transaction.HandleWriteKey(
                ref readWriteStamp,
                in key,
                hasOldValue,
                in oldValue);

            // skip the write based on Thomas Write Rule.
            if (action == OptimisticWriteAction.SkipWrite)
                return TransactionResult<bool>.Success(!hasOldValue);

            // abort case
            if (action == OptimisticWriteAction.Abort)
            {
                AbortTransaction(transaction);
                return TransactionResult<bool>.Aborted();
            }

            // actual write happens.
            TransactionLog.AddOrUpdateReadWriteStamp(key, in readWriteStamp);
            ZoneTree.Upsert(in key, in value);
            return TransactionResult<bool>.Success(!hasOldValue);
        }
    }

    public TransactionResult DeleteNoThrow(long transactionId, in TKey key)
    {
        if (IsReadOnly)
            throw new ZoneTreeIsReadOnlyException();
        lock (this)
        {
            var transaction = GetOrCreateTransactionNoAbortThrow(transactionId);
            if (transaction == null)
                return TransactionResult.Aborted();

            var hasReadWriteStamp = TransactionLog.TryGetReadWriteStamp(key, out var readWriteStamp);
            var hasOldValue = ZoneTree.TryGet(in key, out var oldValue);

            var action = transaction.HandleWriteKey(
               ref readWriteStamp,
               in key,
               hasOldValue,
               in oldValue);

            // skip the write based on Thomas Write Rule.
            if (action == OptimisticWriteAction.SkipWrite)
                return TransactionResult.Success();

            // abort case
            if (action == OptimisticWriteAction.Abort)
            {
                AbortTransaction(transaction);
                return TransactionResult.Aborted();
            }
            TransactionLog.AddOrUpdateReadWriteStamp(key, in readWriteStamp);
            ZoneTree.ForceDelete(in key);
            return TransactionResult.Success();
        }
    }

    public IMaintainer CreateMaintainer()
    {
        return new ZoneTreeMaintainer<TKey, TValue>(this);
    }
}
