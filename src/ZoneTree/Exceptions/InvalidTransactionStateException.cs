using ZoneTree.Transactional;

namespace Tenray;

public class InvalidTransactionStateException : ZoneTreeException
{
    public InvalidTransactionStateException(long transactionId, TransactionState state)
        : base($"Transaction state is not valid for requested operation. {transactionId} State: {state}")
    {
        TransactionId = transactionId;
        State = state;
    }

    public long TransactionId { get; }

    public TransactionState State { get; }
}