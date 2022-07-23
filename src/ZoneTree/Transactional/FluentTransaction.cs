namespace Tenray.ZoneTree.Transactional;

public sealed class FluentTransaction<TKey, TValue> : IDisposable
{
    readonly OptimisticZoneTree<TKey, TValue> ZoneTree;

    long TransactionId;

    readonly List<Func<long, ITransactionResult>> Jobs = new();
    
    int RetryAbortedCount = 10;
    
    int RetryPendingCount = 1000;

    int[] RetryAbortedDelayArray = new int[] { 10, 100, 100, 100, 200, 300, 400, 500, 750, 1000, 2000 };
    
    int[] RetryPendingDelayArray = new int[] { 10, 100, 100, 100, 200, 300, 400, 500, 750, 1000, 2000 };

    public int TotalAbortRetried { get; private set; }
    
    public int TotalPendingTransactionsRetried { get; private set; }

    public FluentTransaction(OptimisticZoneTree<TKey, TValue> zoneTree)
    {
        ZoneTree = zoneTree;
    }

    public FluentTransaction<TKey, TValue> Do(Func<long, ITransactionResult> job)
    {
        Jobs.Add(job);
        return this;
    }

    public FluentTransaction<TKey, TValue> SetRetryCountForAbortedTransactions(int retryCount)
    {
        RetryAbortedCount = retryCount;
        return this;
    }

    public FluentTransaction<TKey, TValue> SetRetryCountForPendingTransactions(int retryCount)
    {
        RetryPendingCount = retryCount;
        return this;
    }

    public FluentTransaction<TKey, TValue> SetAbortedDelayArray(int[] delayArray)
    {
        RetryAbortedDelayArray = delayArray;
        return this;
    }

    public FluentTransaction<TKey, TValue> SetPendingDelayArray(int[] delayArray)
    {
        RetryPendingDelayArray = delayArray;
        return this;
    }

    public async Task<ITransactionResult> CommitAsync()
    {
        var len = RetryAbortedDelayArray.Length;
        var last = RetryAbortedDelayArray[len - 1];
        for (var i = 0; i < RetryAbortedCount; ++i) {            
            var state = RunTransaction();            
            if (state == CommitState.Committed)
                return TransactionResult.Success();
            if (state == CommitState.PendingTransactions)
                return await WaitPendingAndCommit();
            TotalAbortRetried = i + 1;
            var delay = i >= len ? last : RetryAbortedDelayArray[i];
            await Task.Delay(delay);
        }
        return TransactionResult.Aborted();
    }

    CommitState RunTransaction()
    {
        var tx = ZoneTree.BeginTransaction();
        TransactionId = tx;
        foreach (var job in Jobs)
        {
            if (job(tx).IsAborted)
                return CommitState.Aborted;
        }

        var result = ZoneTree.PrepareNoThrow(tx);
        if (result.IsAborted)
            return CommitState.Aborted;

        if (result.IsPendingTransactions)
            return CommitState.PendingTransactions;

        if (ZoneTree.CommitNoThrow(tx).IsAborted)
            return CommitState.Aborted;

        return CommitState.Committed;
    }

    async Task<ITransactionResult> WaitPendingAndCommit()
    {
        var len = RetryPendingDelayArray.Length;
        var last = RetryPendingDelayArray[len - 1];

        var tx = TransactionId;
        for (var i = 0; i < RetryPendingCount; ++i)
        {
            var result = ZoneTree.Prepare(tx);
            if (result.IsAborted)
                return TransactionResult.Aborted();
            if (result.IsReadyToCommit)
            {
                if (ZoneTree.CommitNoThrow(tx).IsCommitted)
                    return TransactionResult.Success();
                return TransactionResult.Aborted();
            }
            TotalPendingTransactionsRetried = i + 1;
            var delay = i >= len ? last : RetryPendingDelayArray[i];
            await Task.Delay(delay);
        }
        ZoneTree.Rollback(tx);
        return TransactionResult.Aborted();
    }

    public void Dispose()
    {
        if (ZoneTree.GetTransactionState(TransactionId) == TransactionState.Uncommitted)
            ZoneTree.Rollback(TransactionId);
    }
}
