using ZoneTree.Transactional;

namespace Tenray;

public class TransactionIsAlreadyCommittedException : ZoneTreeException
{
    public TransactionIsAlreadyCommittedException(long transactionId)
        : base($"Transaction is already committed.")
    {
        TransactionId = transactionId;
    }

    public long TransactionId { get; }
}