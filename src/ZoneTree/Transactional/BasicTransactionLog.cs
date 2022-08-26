using Tenray.ZoneTree.Collections;
using Tenray.ZoneTree.Comparers;
using Tenray.ZoneTree.Core;
using Tenray.ZoneTree.Options;
using Tenray.ZoneTree.Serializers;

namespace Tenray.ZoneTree.Transactional;

public sealed class BasicTransactionLog<TKey, TValue> : ITransactionLog<TKey, TValue>, IDisposable
{
    const string TxMetaCategory = "txm";

    const string TxHistoryCategory = "txh";

    const string TxDependencyCategory = "txd";

    const string TxReadWriteStampCategory = "txs";

    readonly IncrementalIdProvider IncrementalIdProvider = new();

    readonly DictionaryWithWAL<long, TransactionMeta> Transactions;

    readonly DictionaryOfDictionaryWithWAL<long, TKey, CombinedValue<TValue, long>> HistoryTable;

    readonly DictionaryOfDictionaryWithWAL<long, long, bool> DependencyTable;

    readonly DictionaryWithWAL<TKey, ReadWriteStamp> ReadWriteStamps;

    public int CompactionThreshold { get; set; } = 100_000;

    public int TransactionCount
    {
        get
        {
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

    public IReadOnlyList<long> UncommittedTransactionIds
    {
        get
        {
            lock (this)
            {
                var transactionIds = Transactions.Keys;
                return TransactionIds.Where(x =>
                    Transactions.TryGetValue(x, out var meta)
                    && meta.State == TransactionState.Uncommitted)
                .ToArray();
            }
        }
    }

    public BasicTransactionLog(ZoneTreeOptions<TKey, TValue> options)
    {
        var writeAheadLogProvider = options.WriteAheadLogProvider;
        writeAheadLogProvider.InitCategory(TxMetaCategory);
        writeAheadLogProvider.InitCategory(TxHistoryCategory);
        writeAheadLogProvider.InitCategory(TxDependencyCategory);
        Transactions = new(
            0,
            TxMetaCategory,
            writeAheadLogProvider,
            options.WriteAheadLogOptions,
            new Int64Serializer(),
            new StructSerializer<TransactionMeta>(),
            new Int64ComparerAscending(),
            (in TransactionMeta x) => x.StartedAt == 0,
            (ref TransactionMeta x) => x.StartedAt = 0);

        var combinedSerializer = new CombinedSerializer<TValue, long>(options.ValueSerializer, new Int64Serializer());
        HistoryTable = new(
            0,
            TxHistoryCategory,
            writeAheadLogProvider,
            options.WriteAheadLogOptions,
            new Int64Serializer(),
            options.KeySerializer,
            combinedSerializer);

        DependencyTable = new(
            0,
            TxDependencyCategory,
            writeAheadLogProvider,
            options.WriteAheadLogOptions,
            new Int64Serializer(),
            new Int64Serializer(),
            new BooleanSerializer()
            );

        writeAheadLogProvider.InitCategory(TxReadWriteStampCategory);
        ReadWriteStamps = new(
            0,
            TxReadWriteStampCategory,
            options.WriteAheadLogProvider,
            options.WriteAheadLogOptions,
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

            // committed transaction states can be safely dropped from memory.
            DeleteTransactionFromMemory(transactionId);
        }
    }

    public void TransactionStarted(long transactionId)
    {
        lock (this)
        {
            if (Transactions.LogLength > CompactionThreshold)
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
        if (src == dest)
            return;
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

            // Why do we need transaction log compaction?

            // System cannot keep all transaction log forever alive.
            // We should keep the transaction log as small as possible.
            // This ensures stable and fast database access.
            // Auditing entire transaction log is still possible with backup of WAL files
            // and IWriteAheadLog is responsible to get the durable live backups.

            // Transaction Log compaction strategy:

            // Phase 1: delete all unnecessary transaction data from memory.
            // Phase 2: compact write ahead logs to reduce the size of wal. 
            // First phase improves the speed of transactions.
            // Second phase improves the loading speed of the transaction log.

            // What happens to Audit Support?
            // Preserving the backup of the transaction log
            // is IWriteAheadLog's responsibility.

            // Phase 1 steps:

            // 1. Delete committed transaction states.
            // This will not cause any trouble because
            // the GetTransactionState method
            // always returns the state "Committed" for non-existent transaction states.
            // Hence, it doesn't matter to keep the state in memory.
            //
            // Question: Why returning the committed state
            // for possibly aborted transaction is harmless?
            // Answer: Aborted Transaction states are guaranteed to be never queried.
            // Because the aborted transactions do not
            // leave any write stamp after the rollback.
            // Besides that, there is no harm if an aborted transaction leaves a read stamp.
            // Because read stamp is required to abort transactions in write stage.
            DeleteCommittedAndCollectAbortedAndUncommittedTransactions(aborted, uncommitted);

            // 2. We can delete aborted transaction states with the following condition: 
            // There isn't any uncommitted transaction that depends on the aborted one.
            // Because we need a lookup to the aborted transaction state to abort
            // the uncommitted transaction that depends on the aborted one.
            DeleteAbortedTransactions(aborted, uncommitted);

            /// 3. We can delete the entire history of
            /// the aborted and committed transactions.
            /// Because we require the history just 
            /// for the rollback operation of uncommitted transactions.
            /// Committed and aborted transactions can not be rollbacked at all.
            DeleteHistoryOfAbortedAndCommittedTransactions();

            /// 4. We can delete all aborted transactions read-write stamps.
            /// we can delete committed transaction read-write stamps
            /// up to the first uncommitted transaction id to not to break the skip write rule.            
            /// Because the Rollback operation depends
            /// on equality of uncommitted transaction write stamps.
            /// rollback cancel condition: readWriteStamp.WriteStamp != uncommittedTransactionId
            var minimumUncommittedTransactionId = uncommitted.Count == 0 ? long.MaxValue : uncommitted.Min();
            DeleteReadWriteStampsOfAbortedAndCommitted(minimumUncommittedTransactionId);

            // Phase 2 begins.
            AddDummyTransactionToPreserveNextTransactionIdInLog();
            Transactions.CompactWriteAheadLog();
            HistoryTable.CompactWriteAheadLog();
            DependencyTable.CompactWriteAheadLog();
            ReadWriteStamps.CompactWriteAheadLog();
        }
    }

    private void DeleteCommittedAndCollectAbortedAndUncommittedTransactions(
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
                if (dic != null &&
                    dic.ContainsKey(a))
                {
                    isDependentA = true;
                    break;
                }
            }
            if (!isDependentA)
                DeleteTransactionFromMemory(a);
        }
    }

    private void DeleteHistoryOfAbortedAndCommittedTransactions()
    {
        var keys = HistoryTable.Keys;
        foreach (var key in keys)
        {
            if (Transactions.TryGetValue(key, out var meta) &&
                meta.State == TransactionState.Uncommitted)
                continue;
            HistoryTable.TryDeleteFromMemory(key);
        }
    }

    private void DeleteReadWriteStampsOfAbortedAndCommitted(long minimumUncommittedTransactionId)
    {
        var keys = ReadWriteStamps.Keys;
        var values = ReadWriteStamps.Values;
        var len = keys.Length;
        for (var i = 0; i < len; ++i)
        {
            var key = keys[i];
            var value = values[i];
            var writeStamp = value.WriteStamp;
            if (writeStamp >= minimumUncommittedTransactionId)
                continue;

            if (Transactions.TryGetValue(writeStamp, out var meta))
            {
                var state = meta.State;
                if (state == TransactionState.Uncommitted)
                    continue;
            }
            var readStamp = value.ReadStamp;
            if (Transactions.TryGetValue(readStamp, out meta))
            {
                var state = meta.State;
                if (state == TransactionState.Uncommitted)
                    continue;
            }
            ReadWriteStamps.TryDeleteFromMemory(in key);
        }
    }

    private void AddDummyTransactionToPreserveNextTransactionIdInLog()
    {
        // Dummy transaction ensures the next transaction id
        // is not lost on the next load of the transaction log.
        var transactionId = GetNextTransactionId();
        var ticks = DateTime.UtcNow.Ticks;
        var dummyTransaction = new TransactionMeta
        {
            StartedAt = ticks,
            EndedAt = ticks,
            State = TransactionState.Committed
        };
        Transactions.Upsert(transactionId, in dummyTransaction);
    }

    private void DeleteTransactionFromMemory(long id)
    {
        Transactions.TryDeleteFromMemory(id);
        HistoryTable.TryDeleteFromMemory(id);
        DependencyTable.TryDeleteFromMemory(id);
    }

    public IReadOnlyList<long> GetUncommittedTransactionIdsBefore(DateTime dateTime)
    {
        var ticks = dateTime.Ticks;
        lock (this)
        {
            var transactionIds = Transactions.Keys;
            return TransactionIds.Where(x =>
                Transactions.TryGetValue(x, out var meta)
                && meta.State == TransactionState.Uncommitted
                && meta.StartedAt < ticks)
            .ToArray();
        }
    }
}
