namespace Tenray;

public class TransactionIsAbortedException : ZoneTreeException
{
    public TransactionIsAbortedException(long transactionId, TransactionResult result)
        : base($"Transaction is aborted. {transactionId} Reason: {result}")
    {
        TransactionId = transactionId;
        Result = result;
    }

    public long TransactionId { get; }

    public TransactionResult Result { get; }
}