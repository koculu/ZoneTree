namespace Tenray;

public interface ITransactionalZoneTree<TKey, TValue> : IDisposable
{
    IZoneTree<TKey, TValue> ZoneTree { get; }

    long CreateTransactionId();

    bool ContainsKey(long transactionId, in TKey key);

    bool TryGet(long transactionId, in TKey key, out TValue value);

    void Upsert(long transactionId, in TKey key, in TValue value);

    bool TryDelete(long transactionId, in TKey key);

    void ForceDelete(long transactionId, in TKey key);

    TransactionResult CommitTransaction(long transactionId);

    TransactionResult AbortTransaction(long transactionId);
}
