using Tenray;
using Tenray.Collections;
using ZoneTree.Collections;
using ZoneTree.Core;
using ZoneTree.Serializers;
using ZoneTree.WAL;

namespace ZoneTree.Transactional;

public sealed class OptimisticZoneTree<TKey, TValue> : ITransactionalZoneTree<TKey, TValue>
{
    public IZoneTree<TKey, TValue> ZoneTree { get; }

    private const string TxRecCategory = "txrec";

    readonly IncrementalIdProvider IdProvider = new ();

    readonly DictionaryWithWAL<TKey, OptimisticRecord> Records;

    public OptimisticZoneTree(ZoneTreeOptions<TKey, TValue> options)
    {
        ZoneTree = new ZoneTree<TKey, TValue>(options);
        Records = new(
            0,
            TxRecCategory,
            options.WriteAheadLogProvider,
            options.KeySerializer,
            new StructSerializer<OptimisticRecord>(),
            options.Comparer,
            (in OptimisticRecord x) => x.IsDeleted,
            (ref OptimisticRecord x) => x = default);
    }

    private OptimisticTransaction<TKey, TValue> GetOrCreateTransaction(long transactionId)
    {
        return null;
    }

    public TransactionResult AbortTransaction(long transactionId)
    {
        throw new NotImplementedException();
    }

    public TransactionResult CommitTransaction(long transactionId)
    {
        /*
         While there is a transaction DEP(Ti) that has not ended: wait
         If there is a transaction in DEP(Ti) that aborted then abort
         Otherwise: commit.
         */
        throw new NotImplementedException();
    }

    public bool ContainsKey(long transactionId, in TKey key)
    {
        throw new NotImplementedException();
    }

    public long CreateTransactionId()
    {
        return IdProvider.NextId();
    }

    public void Dispose()
    {
        ZoneTree.Dispose();
        Records.Dispose();
    }

    public void ForceDelete(long transactionId, in TKey key)
    {
        throw new NotImplementedException();
    }

    public bool TryDelete(long transactionId, in TKey key)
    {
        throw new NotImplementedException();
    }

    public bool TryGet(long transactionId, in TKey key, out TValue value)
    {
        var transaction = GetOrCreateTransaction(transactionId);
        lock (this)
        {
            var hasOptRecord = Records.TryGetValue(key, out var optRecord);
            var hasValue = ZoneTree.TryGet(in key, out value);
            transaction.HandleReadKey(ref optRecord);
            Records.Upsert(key, in optRecord);
            return hasValue;
        }
    }

    public void Upsert(long transactionId, in TKey key, in TValue value)
    {
        var transaction = GetOrCreateTransaction(transactionId);
        lock (this)
        {
            var hasOptRecord = Records.TryGetValue(key, out var optRecord);

            var hasOldValue = ZoneTree.TryGet(in key, out var oldValue);

            // skip the write based on Thomas Write Rule.
            if (!transaction.HandleWriteKey(
                ref optRecord,
                in key, 
                hasOldValue,
                in oldValue))
                return;

            ZoneTree.Upsert(in key, in value);
            Records.Upsert(key, in optRecord);
        }
    }
}
