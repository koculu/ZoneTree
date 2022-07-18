namespace ZoneTree.Transactional;

public interface ITransactionManager : IDisposable
{
    public int TransactionCount { get; }

    public IReadOnlyList<long> TransactionIds { get; }

    public void TransactionStarted(long transactionId);
    
    public void TransactionCommitted(long transactionId);

    public void TransactionAborted(long transactionId);

    public TransactionState GetTransactionState(long transactionId);

    public TransactionMeta GetTransactionMeta(long transactionId);
}
