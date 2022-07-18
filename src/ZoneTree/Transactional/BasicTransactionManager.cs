using Tenray;
using ZoneTree.Collections;
using ZoneTree.Core;
using ZoneTree.Serializers;
using ZoneTree.WAL;

namespace ZoneTree.Transactional;

public sealed class BasicTransactionManager<TKey, TValue> : ITransactionManager<TKey, TValue>, IDisposable
{
    const string TxMetaCategory = "txm";

    const string TxHistory = "txh";

    const string TxDependency = "txd";

    readonly DictionaryWithWAL<long, TransactionMeta> Transactions;

    readonly DictionaryOfDictionaryWithWAL<long, TKey, CombinedValue<TValue, long>> HistoryTable;

    readonly DictionaryOfDictionaryWithWAL<long, long, bool> DependencyTable;

    public int TransactionCount {
        get {
            lock (this)
            {
                return Transactions.Length;
            }
        }
    }

    public IReadOnlyList<long> TransactionIds
    {
        get
        {
            lock (this)
            {
                return Transactions.Keys;
            }
        }
    }

    public BasicTransactionManager(ZoneTreeOptions<TKey, TValue> options)
    {
        var writeAheadLogProvider = options.WriteAheadLogProvider;
        writeAheadLogProvider.InitCategory(TxMetaCategory);
        writeAheadLogProvider.InitCategory(TxHistory);
        writeAheadLogProvider.InitCategory(TxDependency);
        Transactions = new(
            0,
            TxMetaCategory,
            writeAheadLogProvider,
            new Int64Serializer(),
            new StructSerializer<TransactionMeta>(),
            new Int64ComparerAscending(),
            (in TransactionMeta x) => x.TransactionId == 0,
            (ref TransactionMeta x) => x.TransactionId = 0);

        var combinedSerializer = new CombinedSerializer<TValue, long>(options.ValueSerializer, new Int64Serializer());
        HistoryTable = new(
            0,
            TxHistory,
            writeAheadLogProvider,
            new Int64Serializer(),
            options.KeySerializer,
            combinedSerializer);

        DependencyTable = new(
            0,
            TxDependency,
            writeAheadLogProvider,
            new Int64Serializer(),
            new Int64Serializer(),
            new BooleanSerializer()
            );
    }

    public TransactionMeta GetTransactionMeta(long transactionId)
    {
        lock (this)
        {
            if (Transactions.TryGetValue(transactionId, out var transactionMeta))
                return transactionMeta;
        }
        return default;
    }

    public TransactionState GetTransactionState(long transactionId)
    {
        lock (this)
        {
            if (Transactions.TryGetValue(transactionId, out var transactionMeta))
                return transactionMeta.State;
        }
        // all non-existing transactions are considered uncommitted.
        return TransactionState.Uncommitted;
    }

    public void TransactionAborted(long transactionId)
    {
        lock (this)
        {
            Transactions.TryGetValue(transactionId, out var transactionMeta);
            transactionMeta.EndedAt = DateTime.UtcNow.Ticks;
            transactionMeta.State = TransactionState.Aborted;
            Transactions.Upsert(transactionId, in transactionMeta);
        }
    }

    public void TransactionCommitted(long transactionId)
    {
        lock (this)
        {
            Transactions.TryGetValue(transactionId, out var transactionMeta);            
            transactionMeta.EndedAt = DateTime.UtcNow.Ticks;
            transactionMeta.State = TransactionState.Committed;
            Transactions.Upsert(transactionId, in transactionMeta);
        }
    }

    public void TransactionStarted(long transactionId)
    {
        lock(this)
        {
            if (Transactions.ContainsKey(transactionId))
                return;
            var transactionMeta = new TransactionMeta
            {
                TransactionId = transactionId,
                StartedAt = DateTime.UtcNow.Ticks,
                State = TransactionState.Uncommitted
            };
            Transactions.Upsert(transactionId, in transactionMeta);
        }
    }

    public void Dispose()
    {
        Transactions?.Dispose();
        DependencyTable?.Dispose();
        HistoryTable?.Dispose();
    }

    public void AddDependency(long src, long dest)
    {
        lock (this)
        {
            DependencyTable.Upsert(src, dest, false);
        }
    }

    public void AddHistory(long transactionId, TKey key, CombinedValue<TValue, long> combinedValue)
    {
        lock (this)
        {
            HistoryTable.Upsert(transactionId, key, combinedValue);
        }
    }

    public IDictionary<TKey, CombinedValue<TValue, long>> GetHistory(long transactionId)
    {
        lock (this)
        {
            if (HistoryTable.TryGetDictionary(transactionId, out var history))
                return history;
        }
        return new Dictionary<TKey, CombinedValue<TValue, long>>();
    }

    public IReadOnlyList<long> GetDependencyList(long transactionId)
    {
        lock (this)
        {
            if (DependencyTable.TryGetDictionary(transactionId, out var dic))
                return dic.Keys.ToArray();
            return Array.Empty<long>();
        }
    }
}
