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
    /// Transaction is killed as a deadlock victim.
    /// Note: Optimistic transactions does not produce deadlocks.
    /// </summary>
    DeadlockVictim
}