using System.Diagnostics;
using Tenray;
using ZoneTree.Collections;
using ZoneTree.Core;
using ZoneTree.Serializers;

namespace ZoneTree.Transactional;

public sealed class BasicTransactionLog<TKey, TValue> : ITransactionLog<TKey, TValue>, IDisposable
{
    const string TxMetaCategory = "txm";

    const string TxHistory = "txh";

    const string TxDependency = "txd";

    const string TxReadWriteStampCategory = "txs";

    readonly IncrementalInt64IdProvider IncrementalIdProvider = new();

    readonly DictionaryWithWAL<long, TransactionMeta> Transactions;

    readonly DictionaryOfDictionaryWithWAL<long, TKey, CombinedValue<TValue, long>> HistoryTable;

    readonly DictionaryOfDictionaryWithWAL<long, long, bool> DependencyTable;

    readonly DictionaryWithWAL<TKey, ReadWriteStamp> ReadWriteStamps;

    public int TransactionLogCompactionThreshold { get; set; } = 100000;

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

    public BasicTransactionLog(ZoneTreeOptions<TKey, TValue> options)
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
            (in TransactionMeta x) => x.StartedAt == 0,
            (ref TransactionMeta x) => x.StartedAt = 0);

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

        writeAheadLogProvider.InitCategory(TxReadWriteStampCategory);
        ReadWriteStamps = new(
            0,
            TxReadWriteStampCategory,
            options.WriteAheadLogProvider,
            options.KeySerializer,
            new StructSerializer<ReadWriteStamp>(),
            options.Comparer,
            (in ReadWriteStamp x) => x.IsDeleted,
            (ref ReadWriteStamp x) => x = default);

        var keys = Transactions.Keys;
        if (keys.Length > 0)
            IncrementalIdProvider.SetNextId(keys.Max() + 1);
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
        // all non-existing transactions are considered committed
        // to simplify compacting the transaction log.
        return TransactionState.Committed;
    }

    public void TransactionAborted(long transactionId)
    {
        lock (this)
        {
            Transactions.TryGetValue(transactionId, out var transactionMeta);
            transactionMeta.EndedAt = DateTime.UtcNow.Ticks;
            transactionMeta.State = TransactionState.Aborted;
            Transactions.Upsert(transactionId, in transactionMeta);
            // aborted transactions does not need dependency and history tables.
            // safe to delete from memory.
            DependencyTable.TryDeleteFromMemory(transactionId);
            HistoryTable.TryDeleteFromMemory(transactionId);
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
            
            // committed transactions can be safely dropped from memory.
            DeleteTransactionFromMemory(transactionId);
        }
    }

    public void TransactionStarted(long transactionId)
    {
        lock(this)
        {
            if (Transactions.LogLength > TransactionLogCompactionThreshold)
                CompactTransactionLog();

            if (Transactions.ContainsKey(transactionId))
                return;
            var transactionMeta = new TransactionMeta
            {
                StartedAt = DateTime.UtcNow.Ticks,
                State = TransactionState.Uncommitted
            };
            Transactions.Upsert(transactionId, in transactionMeta);
        }
    }

    public void AddDependency(long src, long dest)
    {
        lock (this)
        {
            DependencyTable.Upsert(src, dest, false);
        }
    }

    public void AddHistoryRecord(long transactionId, TKey key, CombinedValue<TValue, long> combinedValue)
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

    public void Dispose()
    {
        Transactions?.Dispose();
        DependencyTable?.Dispose();
        HistoryTable?.Dispose();
        ReadWriteStamps?.Dispose();
    }

    public long GetNextTransactionId()
    {
        return IncrementalIdProvider.NextId();
    }

    public bool TryGetReadWriteStamp(in TKey key, out ReadWriteStamp readWriteStamp)
    {
        return ReadWriteStamps.TryGetValue(in key, out readWriteStamp);
    }

    public bool AddOrUpdateReadWriteStamp(in TKey key, in ReadWriteStamp readWriteStamp)
    {
        return ReadWriteStamps.Upsert(in key, readWriteStamp);
    }

    public void CompactTransactionLog()
    {
        lock (this)
        {
            var aborted = new List<long>();
            var uncommitted = new List<long>();

            CollectAbortedAndUncommittedTransactions(aborted, uncommitted);

            DeleteAbortedTransactions(aborted, uncommitted);

            DeleteObsoleteReadStamps();

            DeleteObsoleteHistory();
            
            AddDummyTransactionToPreserveNextTransactionIdInLog();
            Transactions.CompactWriteAheadLog();

            HistoryTable.CompactWriteAheadLog();

            DependencyTable.CompactWriteAheadLog();

            ReadWriteStamps.CompactWriteAheadLog();
        }
    }

    private void AddDummyTransactionToPreserveNextTransactionIdInLog()
    {
        // Dummy transaction ensures the next transaction id
        // is not lost on the next load of the transaction log.
        var transactionId = GetNextTransactionId();
        var dummyTransaction = new TransactionMeta
        {
            StartedAt = DateTime.UtcNow.Ticks,
            EndedAt = DateTime.UtcNow.Ticks,
            State = TransactionState.Committed
        };
        Transactions.Upsert(transactionId, in dummyTransaction);
    }

    private void DeleteObsoleteHistory()
    {
        var keys = HistoryTable.Keys;
        foreach (var key in keys)
            if (!Transactions.ContainsKey(key))
                HistoryTable.TryDeleteFromMemory(key);
    }

    private void DeleteObsoleteReadStamps()
    {
        var keys = ReadWriteStamps.Keys;
        var values = ReadWriteStamps.Values;
        var len = keys.Length;
        for (var i = 0; i < len; ++i)
        {
            var key = keys[i];
            var value = values[i];

            if (Transactions.ContainsKey(value.ReadStamp) ||
            Transactions.ContainsKey(value.WriteStamp))
                continue;
            ReadWriteStamps.TryDeleteFromMemory(in key);
        }
    }

    private void CollectAbortedAndUncommittedTransactions(
        List<long> aborted, 
        List<long> uncommitted)
    {
        var transactionIds = Transactions.Keys;
        Array.Sort(transactionIds);
        foreach (var id in transactionIds)
        {
            var state = GetTransactionState(id);
            switch (state)
            {
                case TransactionState.Uncommitted:
                    uncommitted.Add(id);
                    continue;
                case TransactionState.Aborted:
                    aborted.Add(id);
                    break;
                case TransactionState.Committed:
                    DeleteTransactionFromMemory(id);
                    break;
            }
        }
    }

    private void DeleteAbortedTransactions(IReadOnlyList<long> aborted, IReadOnlyList<long> uncommitted)
    {
        foreach (var a in aborted)
        {
            var isDependentA = false;
            foreach (var u in uncommitted)
            {
                DependencyTable.TryGetDictionary(u, out var dic);
                if (dic.ContainsKey(a))
                {
                    isDependentA = true;
                    break;
                }
            }
            if (!isDependentA)
                DeleteTransactionFromMemory(a);
        }
    }

    private void DeleteTransactionFromMemory(long id)
    {
        Transactions.TryDeleteFromMemory(id);
        HistoryTable.TryDeleteFromMemory(id);
        DependencyTable.TryDeleteFromMemory(id);
    }
}
