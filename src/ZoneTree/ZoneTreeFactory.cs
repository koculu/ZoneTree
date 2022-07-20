using Tenray.Collections;
using ZoneTree.Core;
using ZoneTree.Segments.Disk;
using ZoneTree.Transactional;
using ZoneTree.WAL;

namespace Tenray;

public class ZoneTreeFactory<TKey, TValue>
{
    String WalDirectory;

    ITransactionLog<TKey, TValue> TransactionLog;
    
    int InitialSparseArrayLength = 1_000_000;

    public ZoneTreeOptions<TKey, TValue> Options { get; } = new();

    public ZoneTreeFactory<TKey, TValue> SetComparer(IRefComparer<TKey> comparer)
    {
        Options.Comparer = comparer;
        return this;
    }

    public ZoneTreeFactory<TKey, TValue> 
        SetMutableSegmentMaxItemCount(int mutableSegmentMaxItemCount)
    {
        Options.MutableSegmentMaxItemCount = mutableSegmentMaxItemCount;
        return this;
    }

    public ZoneTreeFactory<TKey, TValue>
        SetDataDirectory(string dataDirectory)
    {
        Options.RandomAccessDeviceManager = new RandomAccessDeviceManager(dataDirectory);
        return this;
    }

    public ZoneTreeFactory<TKey, TValue>
        SetRandomAccessDeviceManager(IRandomAccessDeviceManager randomAccessDeviceManager)
    {
        Options.RandomAccessDeviceManager = randomAccessDeviceManager;
        return this;
    }

    public ZoneTreeFactory<TKey, TValue>
        SetKeySerializer(ISerializer<TKey> keySerializer)
    {
        Options.KeySerializer = keySerializer;
        return this;
    }

    public ZoneTreeFactory<TKey, TValue>
        SetValueSerializer(ISerializer<TValue> valueSerializer)
    {
        Options.ValueSerializer = valueSerializer;
        return this;
    }
    
    public ZoneTreeFactory<TKey, TValue>
        SetWriteAheadLogDirectory(string walDirectory)
    {
        WalDirectory = walDirectory;
        return this;
    }

    private void InitWriteAheadLogProvider()
    {
        if (WalDirectory == null || Options.WriteAheadLogProvider != null)
            return;
        Options.WriteAheadLogProvider = new BasicWriteAheadLogProvider(WalDirectory);
    }

    public ZoneTreeFactory<TKey, TValue>
        SetWriteAheadLogProvider(IWriteAheadLogProvider walProvider)
    {
        Options.WriteAheadLogProvider = walProvider;
        return this;
    }

    public ZoneTreeFactory<TKey, TValue>
        SetMarkValueDeletedDelegate(MarkValueDeletedDelegate<TValue> markValueDeleted)
    {
        Options.MarkValueDeleted = markValueDeleted;
        return this;
    }

    public ZoneTreeFactory<TKey, TValue>
        SetIsValueDeletedDelegate(IsValueDeletedDelegate<TValue> isValueDeleted)
    {
        Options.IsValueDeleted = isValueDeleted;
        return this;
    }

    public ZoneTreeFactory<TKey, TValue>
        SetTransactionLog(ITransactionLog<TKey, TValue> transactionLog)
    {
        TransactionLog = transactionLog;
        return this;
    }
    
    public ZoneTreeFactory<TKey, TValue> SetInitialSparseArrayLength(int initialSparseArrayLength)
    {
        InitialSparseArrayLength = initialSparseArrayLength;
        return this;
    }

    public IZoneTree<TKey, TValue> OpenOrCreate()
    {
        InitWriteAheadLogProvider();
        var loader = new ZoneTreeLoader<TKey, TValue>(Options);
        if (loader.ZoneTreeMetaExists)
        {
            var zoneTree = loader.LoadZoneTree();
            zoneTree.Maintenance.DiskSegment.InitSparseArray(InitialSparseArrayLength);
            return zoneTree;
        }
        return new ZoneTree<TKey, TValue>(Options);
    }

    public IZoneTree<TKey, TValue> Create()
    {
        InitWriteAheadLogProvider();
        var loader = new ZoneTreeLoader<TKey, TValue>(Options);
        if (loader.ZoneTreeMetaExists)
            throw new DatabaseAlreadyExistsException();
        return new ZoneTree<TKey, TValue>(Options);
    }

    public IZoneTree<TKey, TValue> Open()
    {
        InitWriteAheadLogProvider();
        var loader = new ZoneTreeLoader<TKey, TValue>(Options);
        if (!loader.ZoneTreeMetaExists)
            throw new DatabaseNotFoundException();
        return loader.LoadZoneTree();
    }

    public ITransactionalZoneTree<TKey, TValue> OpenOrCreateTransactional()
    {
        var zoneTree = OpenOrCreate();
        var transactionLog =
            TransactionLog ??
            new BasicTransactionLog<TKey, TValue>(Options);

        return new OptimisticZoneTree<TKey, TValue>(Options, transactionLog, zoneTree);
    }

    public ITransactionalZoneTree<TKey, TValue> CreateTransactional()
    {
        var zoneTree = Create();
        var transactionLog =
            TransactionLog ??
            new BasicTransactionLog<TKey, TValue>(Options);
        return new OptimisticZoneTree<TKey, TValue>(Options, transactionLog, zoneTree);
    }

    public ITransactionalZoneTree<TKey, TValue> OpenTransactional()
    {
        var zoneTree = Open();
        var transactionLog =
            TransactionLog ??
            new BasicTransactionLog<TKey, TValue>(Options);
        return new OptimisticZoneTree<TKey, TValue>(Options, transactionLog, zoneTree);
    }
}