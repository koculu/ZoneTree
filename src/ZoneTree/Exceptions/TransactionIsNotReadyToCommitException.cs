namespace Tenray;

public class TransactionIsNotReadyToCommitException : ZoneTreeException
{
    public TransactionIsNotReadyToCommitException(long transactionId)
        : base($"Transaction is not ready to commit. You should call Prepare() first. {transactionId}")
    {
        TransactionId = transactionId;
    }

    public long TransactionId { get; }
}

