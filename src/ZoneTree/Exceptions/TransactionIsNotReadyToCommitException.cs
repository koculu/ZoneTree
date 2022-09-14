namespace Tenray.ZoneTree.Exceptions;

public sealed class TransactionIsNotReadyToCommitException : ZoneTreeException
{
    public TransactionIsNotReadyToCommitException(long transactionId)
        : base($"Transaction is not ready to commit.\r\nYou should call Prepare() first.\r\nTransaction Id: {transactionId}")
    {
        TransactionId = transactionId;
    }

    public long TransactionId { get; }
}

