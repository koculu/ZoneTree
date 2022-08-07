using Tenray.ZoneTree.Exceptions;
using Tenray.ZoneTree.Segments.Disk;
using Tenray.ZoneTree.Transactional;
using Tenray.ZoneTree.WAL;
using Tenray.ZoneTree.Collections;
using Tenray.ZoneTree.Core;
using Tenray.ZoneTree.AbstractFileStream;

namespace Tenray.ZoneTree;

public class ZoneTreeFactory<TKey, TValue>
{
    string WalDirectory;

    int InitialSparseArrayLength = 1_000_000;

    readonly IFileStreamProvider FileStreamProvider;

    Func<ZoneTreeOptions<TKey, TValue>, IWriteAheadLogProvider> GetWriteAheadLogProvider;

    Func<ZoneTreeOptions<TKey, TValue>, ITransactionLog<TKey, TValue>> GetTransactionLog
        = (options) => new BasicTransactionLog<TKey, TValue>(options);

    ITransactionLog<TKey, TValue> TransactionLog;

    public ZoneTreeOptions<TKey, TValue> Options { get; } = new();


    public ZoneTreeFactory(IFileStreamProvider fileStreamProvider = null)
    {
        if (fileStreamProvider == null)
            fileStreamProvider = new LocalFileStreamProvider();
        FileStreamProvider = fileStreamProvider;

        GetWriteAheadLogProvider = (options) =>
            WalDirectory == null ? 
            new BasicWriteAheadLogProvider(fileStreamProvider) :
            new BasicWriteAheadLogProvider(fileStreamProvider, WalDirectory);
    }

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
        Options.RandomAccessDeviceManager = new RandomAccessDeviceManager(
            FileStreamProvider, dataDirectory);
        return this;
    }

    public ZoneTreeFactory<TKey, TValue>
        SetDiskSegmentCompression(bool enabled)
    {
        Options.EnableDiskSegmentCompression = enabled;
        return this;
    }

    public ZoneTreeFactory<TKey, TValue>
        SetDiskSegmentCompressionBlockSize(int blockSize)
    {
        if (blockSize < 8 * 1024)
            throw new Exception("Compression Block size cannot be smaller than 8KB");
        if (blockSize > 1024 * 1024 * 1024)
            throw new Exception("Compression Block size cannot be greater than 1GB");
        Options.DiskSegmentCompressionBlockSize = blockSize;
        return this;
    }

    public ZoneTreeFactory<TKey, TValue>
        SetDiskSegmentMaximumCachedBlockCount(int diskSegmentBlockCacheLimit)
    {
        if (diskSegmentBlockCacheLimit < 1)
            diskSegmentBlockCacheLimit = 1;
        Options.DiskSegmentBlockCacheLimit = diskSegmentBlockCacheLimit;
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
        if (Options.WriteAheadLogProvider != null)
            return;
        Options.WriteAheadLogProvider = GetWriteAheadLogProvider(Options);
    }

    private void InitTransactionLog()
    {
        if (TransactionLog != null)
            return;
        TransactionLog = GetTransactionLog(Options);
    }

    public ZoneTreeFactory<TKey, TValue>
        SetWriteAheadLogProvider(Func<ZoneTreeOptions<TKey, TValue>, IWriteAheadLogProvider> walProviderGetter)
    {
        GetWriteAheadLogProvider = walProviderGetter;
        return this;
    }

    public ZoneTreeFactory<TKey, TValue>
        Configure(Action<ZoneTreeOptions<TKey, TValue>> configure)
    {
        configure(Options);
        return this;
    }

    public ZoneTreeFactory<TKey, TValue>
        ConfigureWriteAheadLogProvider(Action<IWriteAheadLogProvider> configure)
    {
        InitWriteAheadLogProvider();
        configure(Options.WriteAheadLogProvider);
        return this;
    }

    public ZoneTreeFactory<TKey, TValue>
        ConfigureTransactionLog(Action<ITransactionLog<TKey, TValue>> configure)
    {
        InitTransactionLog();
        configure(TransactionLog);
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
        SetTransactionLog(Func<ZoneTreeOptions<TKey, TValue>, ITransactionLog<TKey, TValue>> transactionLogGetter)
    {
        GetTransactionLog = transactionLogGetter;
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
        InitTransactionLog();
        return new OptimisticZoneTree<TKey, TValue>(Options, TransactionLog, zoneTree);
    }

    public ITransactionalZoneTree<TKey, TValue> CreateTransactional()
    {
        var zoneTree = Create(); 
        InitTransactionLog();
        return new OptimisticZoneTree<TKey, TValue>(Options, TransactionLog, zoneTree);
    }

    public ITransactionalZoneTree<TKey, TValue> OpenTransactional()
    {
        var zoneTree = Open();
        InitTransactionLog();
        return new OptimisticZoneTree<TKey, TValue>(Options, TransactionLog, zoneTree);
    }
}