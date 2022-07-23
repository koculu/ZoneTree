namespace Tenray.ZoneTree.Transactional;

public enum CommitState
{
    /// <summary>
    /// Transaction is aborted.
    /// </summary>
    Aborted,

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
    /// Transaction can not commit due to other uncommitted transactions.
    /// </summary>
    PendingTransactions
}