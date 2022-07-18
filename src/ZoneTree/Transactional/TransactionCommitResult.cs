using Tenray;

namespace ZoneTree.Transactional;

public class TransactionCommitResult
{
    public readonly static TransactionCommitResult CommittedResult = new(TransactionResult.Committed);

    /// <summary>
    /// Transaction result.
    /// </summary>
    public TransactionResult Result { get; }

    /// <summary>
    /// If transaction result is "WaitUncommittedTransactions"
    /// this list is the transactions that are uncommitted.
    /// They should commit before this transaction commits.
    /// If any of the pending transactions abort,
    /// this transaction also aborts.
    /// </summary>
    public IReadOnlyList<long> PendingTransactionList { get; }

    public bool IsCommitted => Result == TransactionResult.Committed;

    public bool IsAbortedRetry => Result == TransactionResult.AbortedRetry;

    public bool IsAborted => Result == TransactionResult.AbortedDontRetry;

    public bool IsWaitingUncommittedTransactions => Result == TransactionResult.WaitUncommittedTransactions;

    public TransactionCommitResult(
        TransactionResult result,
        IReadOnlyList<long> pendingTransactionsList = null)
    {
        Result = result;
        PendingTransactionList = pendingTransactionsList;
    }
}