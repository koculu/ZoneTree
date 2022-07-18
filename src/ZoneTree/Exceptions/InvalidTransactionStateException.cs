using ZoneTree.Transactional;

namespace Tenray;

public class TransactionIsAlreadyCommittedException : ZoneTreeException
{
    public TransactionIsAlreadyCommittedException(long transactionId)
        : base($"Transaction is already committed. Transaction Id: {transactionId}")
    {
        TransactionId = transactionId;
    }

    public long TransactionId { get; }
}