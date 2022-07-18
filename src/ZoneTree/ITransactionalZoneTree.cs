using ZoneTree.Transactional;

namespace Tenray;

public interface ITransactionalZoneTree<TKey, TValue> : IDisposable
{
    IZoneTree<TKey, TValue> ZoneTree { get; }

    long CreateTransactionId();

    bool ContainsKey(long transactionId, in TKey key);

    bool TryGet(long transactionId, in TKey key, out TValue value);

    bool Upsert(long transactionId, in TKey key, in TValue value);

    void Delete(long transactionId, in TKey key);

    TransactionCommitResult CommitTransaction(long transactionId);

    void AbortTransaction(long transactionId);
}
