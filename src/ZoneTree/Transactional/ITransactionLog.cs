using Tenray.ZoneTree.Serializers;

namespace Tenray.ZoneTree.Transactional;

public interface ITransactionLog<TKey, TValue> : IDisposable
{
    /// <summary>
    /// The total amount of transactions to be kept in memory.
    /// </summary>
    int CompactionThreshold { get; set; }

    int TransactionCount { get; }

    IReadOnlyList<long> TransactionIds { get; }

    IReadOnlyList<long> UncommittedTransactionIds { get; }

    long GetNextTransactionId();

    void TransactionStarted(long transactionId);

    void TransactionCommitted(long transactionId);

    void TransactionAborted(long transactionId);

    TransactionState GetTransactionState(long transactionId);

    TransactionMeta GetTransactionMeta(long transactionId);

    void AddDependency(long src, long dest);

    IReadOnlyList<long> GetDependencyList(long transactionId);

    void AddHistoryRecord(
        long transactionId,
        TKey key,
        CombinedValue<TValue, long> combinedValue);

    IDictionary<TKey, CombinedValue<TValue, long>> GetHistory(long transactionId);

    bool TryGetReadWriteStamp(in TKey key, out ReadWriteStamp readWriteStamp);

    bool AddOrUpdateReadWriteStamp(in TKey key, in ReadWriteStamp readWriteStamp);
}