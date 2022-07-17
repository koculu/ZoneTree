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
    AbortedDontRetry
}