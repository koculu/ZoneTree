using Tenray;
using ZoneTree.Collections;
using ZoneTree.Serializers;
using ZoneTree.WAL;

namespace ZoneTree.Transactional;

public sealed class BasicTransactionManager : ITransactionManager, IDisposable
{
    private const string TxMetaCategory = "txm";
    readonly DictionaryWithWAL<long, TransactionMeta> Transactions;

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

    public BasicTransactionManager(IWriteAheadLogProvider writeAheadLogProvider)
    {
        writeAheadLogProvider.InitCategory(TxMetaCategory);
        Transactions = new(
            0,
            TxMetaCategory,
            writeAheadLogProvider,
            new Int64Serializer(),
            new StructSerializer<TransactionMeta>(),
            new Int64ComparerAscending(),
            (in TransactionMeta x) => x.TransactionId == 0,
            (ref TransactionMeta x) => x.TransactionId = 0);
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
    }

}
