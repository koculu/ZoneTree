namespace Tenray;

public enum TransactionResult
{
    /// <summary>
    /// Successful commit result.
    /// </summary>
    Committed,

    /// <summary>
    /// Transaction is aborted.
    /// Retry is caller's responsibility.
    /// </summary>
    AbortedRetry,

    /// <summary>
    /// Transaction is aborted.
    /// </summary>
    AbortedDontRetry,

    /// <summary>
    /// Transaction can not commit due to other uncommitted transactions.
    /// </summary>
    WaitUncommittedTransactions
}