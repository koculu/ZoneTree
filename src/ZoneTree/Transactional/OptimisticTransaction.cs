using Tenray;
using ZoneTree.Collections;
using ZoneTree.Core;
using ZoneTree.Serializers;

namespace ZoneTree.Transactional;

public sealed class OptimisticTransaction<TKey, TValue> : IDisposable
{
    const string TxHistory = "txh";

    const string TxDependency = "txd";

    public long TransactionId { get; }

    readonly DictionaryWithWAL<TKey, CombinedValue<TValue, long>> OldValueTable;
    
    readonly DictionaryWithWAL<long, bool> DependencyTable;

    readonly ZoneTreeOptions<TKey, TValue> Options;

    public IEnumerable<KeyValuePair<TKey, CombinedValue<TValue, long>>> 
        OldValueEnumerable => OldValueTable.Enumerable;

    public bool IsReadyToCommit { get; set; }

    public OptimisticTransaction(
        long transactionId,
        ZoneTreeOptions<TKey, TValue> options)
    {
        TransactionId = transactionId;
        Options = options;
        OldValueTable = new(
            transactionId,
            TxHistory,
            options.WriteAheadLogProvider,
            options.KeySerializer,
            new CombinedSerializer<TValue, long>(
                options.ValueSerializer,
                new Int64Serializer()),
            options.Comparer,
            (in CombinedValue<TValue, long> x) => x.Value2 == 0,
            (ref CombinedValue<TValue, long> x) => x.Value2 = 0
            );

        DependencyTable = new(
            transactionId,
            TxDependency,
            options.WriteAheadLogProvider,
            new Int64Serializer(),
            new BooleanSerializer(),
            new Int64ComparerAscending(),
            (in bool x) => x == true,
            (ref bool x) => x = true
            );
    }

    public void Drop()
    {
        DependencyTable.Drop();
        OldValueTable.Drop();
    }

    /// <summary>
    /// Marks read stamp, adds dependencies or aborts transaction.
    /// https://en.wikipedia.org/wiki/Timestamp-based_concurrency_control
    /// </summary>
    /// <param name="readWriteStamp"></param>
    /// <returns>Optimistic read action.</returns>
    public OptimisticReadAction HandleReadKey(
        ref ReadWriteStamp readWriteStamp)
    {
        if (readWriteStamp.WriteStamp > TransactionId)
        {
            return OptimisticReadAction.Abort;
        }

        if (readWriteStamp.WriteStamp != 0)
            DependencyTable.Upsert(readWriteStamp.WriteStamp, false);
        readWriteStamp.ReadStamp = Math.Max(readWriteStamp.ReadStamp, TransactionId);
        return OptimisticReadAction.Read;
    }

    /// <summary>
    /// Marks write stamp, adds old values or aborts transaction.
    /// Returns SkipWrite for skipping writes. (Thomas Write Rule)
    /// https://en.wikipedia.org/wiki/Thomas_Write_Rule
    /// </summary>
    /// <param name="readWriteStamp"></param>
    /// <param name="key"></param>
    /// <param name="hasOldValue"></param>
    /// <param name="oldValue"></param>
    /// <returns>Optimistic write action</returns>
    public OptimisticWriteAction HandleWriteKey(
        ref ReadWriteStamp readWriteStamp, 
        in TKey key,
        bool hasOldValue, 
        in TValue oldValue)
    {
        if (readWriteStamp.ReadStamp > TransactionId)
        {
            return OptimisticWriteAction.Abort;
        }

        if (readWriteStamp.WriteStamp > TransactionId)
            return OptimisticWriteAction.SkipWrite;

        var value = oldValue;
        if (!hasOldValue)
            Options.MarkValueDeleted(ref value);

        var combinedValue = 
            new CombinedValue<TValue, long>(value, readWriteStamp.WriteStamp);
        OldValueTable.Upsert(key, combinedValue);
        readWriteStamp.WriteStamp = TransactionId;
        return OptimisticWriteAction.Write;
    }

    public IReadOnlyList<long> GetDependencyList()
    {
        return DependencyTable.Keys;
    }

    public void Dispose()
    {
        DependencyTable?.Dispose();
        OldValueTable?.Dispose();
    }
}
