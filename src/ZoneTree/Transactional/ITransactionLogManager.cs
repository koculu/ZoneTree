using ZoneTree.Serializers;

namespace ZoneTree.Transactional;

public interface ITransactionLog<TKey, TValue> : IDisposable
{
    int TransactionCount { get; }

    IReadOnlyList<long> TransactionIds { get; }

    void TransactionStarted(long transactionId);
    
    void TransactionCommitted(long transactionId);

    void TransactionAborted(long transactionId);

    TransactionState GetTransactionState(long transactionId);

    TransactionMeta GetTransactionMeta(long transactionId);
    
    void AddDependency(long src, long dest);
    
    void AddHistory(long transactionId, TKey key, CombinedValue<TValue, long> combinedValue);
    
    IDictionary<TKey, CombinedValue<TValue, long>> GetHistory(long transactionId);
    
    IReadOnlyList<long> GetDependencyList(long transactionId);
}