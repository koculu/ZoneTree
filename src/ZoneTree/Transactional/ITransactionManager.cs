namespace ZoneTree.Transactional;

public interface ITransactionManager
{
    public void TransactionStarted(long transactionId);
    
    public void TransactionCommitted(long transactionId);

    public void TransactionAborted(long transactionId);

    public TransactionState GetTransactionState(long transactionId);
}
