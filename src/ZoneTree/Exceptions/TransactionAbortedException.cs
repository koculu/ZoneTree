namespace Tenray.ZoneTree.Exceptions;

public sealed class TransactionAbortedException : ZoneTreeException
{
    public TransactionAbortedException(long transactionId)
        : base($"Transaction is aborted. Transaction Id: {transactionId}")
    {
        TransactionId = transactionId;
    }

    public long TransactionId { get; }
}
