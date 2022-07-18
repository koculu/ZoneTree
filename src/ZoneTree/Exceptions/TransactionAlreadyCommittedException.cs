using ZoneTree.Transactional;

namespace Tenray;

public class TransactionAlreadyCommittedException : ZoneTreeException
{
    public TransactionAlreadyCommittedException(long transactionId)
        : base($"Transaction is already committed. Transaction Id: {transactionId}")
    {
        TransactionId = transactionId;
    }

    public long TransactionId { get; }
}