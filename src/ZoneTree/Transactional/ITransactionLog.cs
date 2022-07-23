using Tenray.ZoneTree.Serializers;

namespace Tenray.ZoneTree.Transactional;

public interface ITransactionLog<TKey, TValue> : IDisposable
{
    /// <summary>
    /// The total amount of transactions to be kept in memory.
    /// </summary>
    int CompactionThreshold { get; set; }

    /// <summary>
    /// Transaction count in the memory.
    /// </summary>
    int TransactionCount { get; }

    /// <summary>
    /// Return all transaction ids that are available in memory.
    /// </summary>
    IReadOnlyList<long> TransactionIds { get; }

    /// <summary>
    /// Returns all uncommitted transaction ids.
    /// </summary>
    IReadOnlyList<long> UncommittedTransactionIds { get; }

    /// <summary>
    /// Retrieves all uncommitted transaction ids started before given date-time.
    /// This method can be used to gather stale uncommitted transaction ids.
    /// </summary>
    /// <param name="dateTime">Max start time (exclusive)</param>
    /// <returns>Uncommitted ids</returns>
    IReadOnlyList<long> GetUncommittedTransactionIdsBefore(DateTime dateTime);

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