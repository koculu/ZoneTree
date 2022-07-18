using Tenray.Collections;
using ZoneTree.Core;
using ZoneTree.Segments.Disk;
using ZoneTree.Transactional;
using ZoneTree.WAL;

namespace Tenray;

public class ZoneTreeFactory<TKey, TValue>
{
    String WalDirectory;

    ITransactionManager TransactionManager;

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
        SetWriteAheadLogProvider(
        Func<ZoneTreeOptions<TKey, TValue>, IWriteAheadLogProvider> walProvider)
    {
        Options.WriteAheadLogProvider = walProvider(Options);
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
        SetTransactionManager(ITransactionManager transactionManager)
    {
        TransactionManager = transactionManager;
        return this;
    }

    public IZoneTree<TKey, TValue> OpenOrCreate()
    {
        InitWriteAheadLogProvider();
        var loader = new ZoneTreeLoader<TKey, TValue>(Options);
        if (loader.ZoneTreeMetaExists)
        {
            return loader.LoadZoneTree();
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
        var transactionManager =
            TransactionManager ??
            new BasicTransactionManager(Options.WriteAheadLogProvider);

        return new OptimisticZoneTree<TKey, TValue>(Options, transactionManager, zoneTree);
    }

    public ITransactionalZoneTree<TKey, TValue> CreateTransactional()
    {
        var zoneTree = Create();
        var transactionManager =
            TransactionManager ??
            new BasicTransactionManager(Options.WriteAheadLogProvider);
        return new OptimisticZoneTree<TKey, TValue>(Options, transactionManager, zoneTree);
    }

    public ITransactionalZoneTree<TKey, TValue> OpenTransactional()
    {
        var zoneTree = Open();
        var transactionManager =
            TransactionManager ??
            new BasicTransactionManager(Options.WriteAheadLogProvider);
        return new OptimisticZoneTree<TKey, TValue>(Options, transactionManager, zoneTree);
    }
}