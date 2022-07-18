using ZoneTree.Transactional;

namespace Tenray;

public class TransactionIsAbortedException : ZoneTreeException
{
    public TransactionIsAbortedException(long transactionId)
        : base($"Transaction is aborted. Transaction Id: {transactionId}")
    {
        TransactionId = transactionId;
    }

    public long TransactionId { get; }
}