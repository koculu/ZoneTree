namespace Tenray;

public enum TransactionResult
{
    /// <summary>
    /// Transaction is in committable stage.
    /// At this stage the user can abort the transaction
    /// or can complete the transaction commit.
    /// </summary>
    ReadyToCommit,

    /// <summary>
    /// Successful commit result.
    /// </summary>
    Committed,

    /// <summary>
    /// Transaction is aborted.
    /// Retry is caller's responsibility.
    /// </summary>
    Aborted,

    /// <summary>
    /// Transaction can not commit due to other uncommitted transactions.
    /// </summary>
    WaitUncommittedTransactions
}