using ZoneTree.Transactional;

namespace Tenray;

public class TransactionIsAbortedException : ZoneTreeException
{
    public TransactionIsAbortedException(long transactionId)
        : base($"Transaction is aborted. {transactionId}")
    {
        TransactionId = transactionId;
    }

    public long TransactionId { get; }
}