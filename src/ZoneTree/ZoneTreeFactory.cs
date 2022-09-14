using Tenray.ZoneTree.Exceptions;
using Tenray.ZoneTree.Segments.Disk;
using Tenray.ZoneTree.Transactional;
using Tenray.ZoneTree.WAL;
using Tenray.ZoneTree.Core;
using Tenray.ZoneTree.AbstractFileStream;
using Tenray.ZoneTree.Comparers;
using Tenray.ZoneTree.Serializers;
using Tenray.ZoneTree.Options;
using Tenray.ZoneTree.Logger;

namespace Tenray.ZoneTree;

/// <summary>
/// The factory to open or create a ZoneTree.
/// </summary>
/// <typeparam name="TKey">The key type</typeparam>
/// <typeparam name="TValue">The value type</typeparam>
public sealed class ZoneTreeFactory<TKey, TValue>
{
    string WalDirectory;

    int InitialSparseArrayLength = 1_000_000;

    readonly IFileStreamProvider FileStreamProvider;

    Func<ZoneTreeOptions<TKey, TValue>, IWriteAheadLogProvider> GetWriteAheadLogProvider;

    Func<ZoneTreeOptions<TKey, TValue>, ITransactionLog<TKey, TValue>> GetTransactionLog
        = (options) => new BasicTransactionLog<TKey, TValue>(options);

    ITransactionLog<TKey, TValue> TransactionLog;

    /// <summary>
    /// The options to be configured by this factory.
    /// </summary>
    public ZoneTreeOptions<TKey, TValue> Options { get; private set; } = new();

    /// <summary>
    /// Creates a new factory.
    /// </summary>
    /// <param name="fileStreamProvider">The FileStreamProvider. When it is not given, the LocalFileStreamProvider is used.</param>
    public ZoneTreeFactory(IFileStreamProvider fileStreamProvider = null)
    {
        if (fileStreamProvider == null)
            fileStreamProvider = new LocalFileStreamProvider();
        FileStreamProvider = fileStreamProvider;

        GetWriteAheadLogProvider = (options) =>
            WalDirectory == null ? 
            new WriteAheadLogProvider(options.Logger, fileStreamProvider) :
            new WriteAheadLogProvider(options.Logger, fileStreamProvider, WalDirectory);
    }

    /// <summary>
    /// Assigns a logger for ZoneTree.
    /// </summary>
    /// <param name="logger"></param>
    /// <returns>ZoneTree Factory</returns>
    public ZoneTreeFactory<TKey, TValue> SetLogger(ILogger logger)
    {
        Options.Logger = logger;
        return this;
    }

    /// <summary>
    /// Sets log level of current ZoneTree logger.
    /// </summary>
    /// <param name="logLevel"></param>
    /// <returns>ZoneTree Factory</returns>
    public ZoneTreeFactory<TKey, TValue> SetLogLevel(LogLevel logLevel)
    {
        Options.Logger.LogLevel = logLevel;
        return this;
    }

    /// <summary>
    /// Sets the key-comparer.
    /// </summary>
    /// <param name="comparer">The key-comparer.</param>
    /// <returns>ZoneTree Factory</returns>
    public ZoneTreeFactory<TKey, TValue> SetComparer(IRefComparer<TKey> comparer)
    {
        Options.Comparer = comparer;
        return this;
    }

    /// <summary>
    /// Configures mutable segment maximum item count.
    /// </summary>
    /// <param name="mutableSegmentMaxItemCount">The maximum item count</param>
    /// <returns>ZoneTree Factory</returns>
    public ZoneTreeFactory<TKey, TValue>
        SetMutableSegmentMaxItemCount(int mutableSegmentMaxItemCount)
    {
        Options.MutableSegmentMaxItemCount = mutableSegmentMaxItemCount;
        return this;
    }

    /// <summary>
    /// Configures disk segment maximum item count.
    /// </summary>
    /// <param name="diskSegmentMaxItemCount">The maximum item count</param>
    /// <returns>ZoneTree Factory</returns>
    public ZoneTreeFactory<TKey, TValue>
        SetDiskSegmentMaxItemCount(int diskSegmentMaxItemCount)
    {
        Options.DiskSegmentMaxItemCount = diskSegmentMaxItemCount;
        return this;
    }

    /// <summary>
    /// Sets the data directory. Default is "./data"
    /// </summary>
    /// <param name="dataDirectory">The data directory</param>
    /// <returns>ZoneTree Factory</returns>
    public ZoneTreeFactory<TKey, TValue>
        SetDataDirectory(string dataDirectory)
    {
        Options.RandomAccessDeviceManager = new RandomAccessDeviceManager(
            Options.Logger,
            FileStreamProvider, dataDirectory);
        WalDirectory = dataDirectory;
        return this;
    }

    /// <summary>
    /// Enables or disables the disk segment compression.
    /// </summary>
    /// <param name="enabled">If true the compression is enabled, otherwise the compression is disabled.</param>
    /// <returns>ZoneTree Factory</returns>
    public ZoneTreeFactory<TKey, TValue>
        SetDiskSegmentCompression(bool enabled)
    {
        Options.DiskSegmentOptions.EnableCompression = enabled;
        return this;
    }

    /// <summary>
    /// Configures the disk segment compression block size.
    /// </summary>
    /// <param name="blockSize">The block size</param>
    /// <returns>ZoneTree Factory</returns>
    /// <exception cref="Exception">Thrown when compression block size is not valid.</exception>
    public ZoneTreeFactory<TKey, TValue>
        SetDiskSegmentCompressionBlockSize(int blockSize)
    {
        if (blockSize < 8 * 1024)
            throw new Exception("Compression Block size cannot be smaller than 8KB");
        if (blockSize > 1024 * 1024 * 1024)
            throw new Exception("Compression Block size cannot be greater than 1GB");
        Options.DiskSegmentOptions.CompressionBlockSize = blockSize;
        return this;
    }

    /// <summary>
    /// Sets configuration options.
    /// </summary>
    /// <param name="options">The configuration options.</param>
    /// <returns>ZoneTree Factory</returns>
    public ZoneTreeFactory<TKey, TValue> SetOptions(ZoneTreeOptions<TKey, TValue> options)
    {
        Options = options;
        return this;
    }

    /// <summary>
    /// Sets the maximum cached block count.
    /// </summary>
    /// <param name="diskSegmentBlockCacheLimit">The maximum cached block count.</param>
    /// <returns>ZoneTree Factory</returns>
    public ZoneTreeFactory<TKey, TValue>
        SetDiskSegmentMaximumCachedBlockCount(int diskSegmentBlockCacheLimit)
    {
        if (diskSegmentBlockCacheLimit < 1)
            diskSegmentBlockCacheLimit = 1;
        Options.DiskSegmentOptions.BlockCacheLimit = diskSegmentBlockCacheLimit;
        return this;
    }

    /// <summary>
    /// Sets random access device manager.
    /// </summary>
    /// <param name="randomAccessDeviceManager">The random access device manager.</param>
    /// <returns>ZoneTree Factory</returns>
    public ZoneTreeFactory<TKey, TValue>
        SetRandomAccessDeviceManager(IRandomAccessDeviceManager randomAccessDeviceManager)
    {
        Options.RandomAccessDeviceManager = randomAccessDeviceManager;
        return this;
    }

    /// <summary>
    /// Sets the key serializer.
    /// </summary>
    /// <param name="keySerializer">The key serializer</param>
    /// <returns>ZoneTree Factory</returns>
    public ZoneTreeFactory<TKey, TValue>
        SetKeySerializer(ISerializer<TKey> keySerializer)
    {
        if (Options.WriteAheadLogProvider != null)
            throw new CannotSetSerializersAfterWriteAheadLogProviderInitializedException();

        if (TransactionLog != null)
            throw new CannotSetSerializersAfterTransactionLogInitializedException();

        Options.KeySerializer = keySerializer;
        return this;
    }

    /// <summary>
    /// Sets the value serializer.
    /// </summary>
    /// <param name="valueSerializer">The value serializer</param>
    /// <returns>ZoneTree Factory</returns>
    public ZoneTreeFactory<TKey, TValue>
        SetValueSerializer(ISerializer<TValue> valueSerializer)
    {
        if (Options.WriteAheadLogProvider != null)
            throw new CannotSetSerializersAfterWriteAheadLogProviderInitializedException();

        if (TransactionLog != null)
            throw new CannotSetSerializersAfterTransactionLogInitializedException();

        Options.ValueSerializer = valueSerializer;
        return this;
    }

    /// <summary>
    /// Sets write ahead log directory.
    /// </summary>
    /// <param name="walDirectory">The write ahead log directory.</param>
    /// <returns>ZoneTree Factory</returns>
    public ZoneTreeFactory<TKey, TValue>
        SetWriteAheadLogDirectory(string walDirectory)
    {
        WalDirectory = walDirectory;
        return this;
    }

    void InitWriteAheadLogProvider()
    {
        if (Options.WriteAheadLogProvider != null)
            return;
        FillMissingOptionsForKnownTypes();
        Options.WriteAheadLogProvider = GetWriteAheadLogProvider(Options);
    }

    void InitTransactionLog()
    {
        if (TransactionLog != null)
            return;
        FillMissingOptionsForKnownTypes();
        TransactionLog = GetTransactionLog(Options);
    }

    /// <summary>
    /// Sets write ahead log provider getter.
    /// </summary>
    /// <param name="walProviderGetter">The write ahead log provider getter.</param>
    /// <returns>ZoneTree Factory</returns>
    public ZoneTreeFactory<TKey, TValue>
        SetWriteAheadLogProvider(Func<ZoneTreeOptions<TKey, TValue>, IWriteAheadLogProvider> walProviderGetter)
    {
        GetWriteAheadLogProvider = walProviderGetter;
        return this;
    }

    /// <summary>
    /// Configures ZoneTree options.
    /// </summary>
    /// <param name="configure">The options configurator delegate</param>
    /// <returns>ZoneTree Factory</returns>
    public ZoneTreeFactory<TKey, TValue>
        Configure(Action<ZoneTreeOptions<TKey, TValue>> configure)
    {
        configure(Options);
        return this;
    }

    /// <summary>
    /// Configures the write ahead log options.
    /// </summary>
    /// <param name="configure">The write ahead log options configurator delegate</param>
    /// <returns>ZoneTree Factory</returns>
    public ZoneTreeFactory<TKey, TValue>
        ConfigureWriteAheadLogOptions(Action<WriteAheadLogOptions> configure)
    {
        configure(Options.WriteAheadLogOptions);
        return this;
    }

    /// <summary>
    /// Configures the disk segment options.
    /// </summary>
    /// <param name="configure">The disk segment options configurator delegate</param>
    /// <returns>ZoneTree Factory</returns>
    public ZoneTreeFactory<TKey, TValue>
        ConfigureDiskSegmentOptions(Action<DiskSegmentOptions> configure)
    {
        configure(Options.DiskSegmentOptions);
        return this;
    }
    /// <summary>
    /// Configures the transaction log.
    /// </summary>
    /// <param name="configure">The transaction log configurator delegate</param>
    /// <returns>ZoneTree Factory</returns>
    public ZoneTreeFactory<TKey, TValue>
        ConfigureTransactionLog(Action<ITransactionLog<TKey, TValue>> configure)
    {
        InitWriteAheadLogProvider();
        InitTransactionLog();
        configure(TransactionLog);
        return this;
    }

    /// <summary>
    /// Assigns value deletion marker delegate.
    /// </summary>
    /// <param name="markValueDeleted">The value deletion marker delegate</param>
    /// <returns>ZoneTree Factory</returns>
    public ZoneTreeFactory<TKey, TValue>
        SetMarkValueDeletedDelegate(MarkValueDeletedDelegate<TValue> markValueDeleted)
    {
        Options.MarkValueDeleted = markValueDeleted;
        return this;
    }

    /// <summary>
    /// Assigns value deletion query delegate.
    /// </summary>
    /// <param name="isValueDeleted">The value deleted query delagate.</param>
    /// <returns>ZoneTree Factory</returns>
    public ZoneTreeFactory<TKey, TValue>
        SetIsValueDeletedDelegate(IsValueDeletedDelegate<TValue> isValueDeleted)
    {
        Options.IsValueDeleted = isValueDeleted;
        return this;
    }

    /// <summary>
    /// Sets the transaction log creator delegate.
    /// </summary>
    /// <param name="transactionLogGetter">The transaction log creator delegate</param>
    /// <returns>ZoneTree Factory</returns>
    public ZoneTreeFactory<TKey, TValue>
        SetTransactionLog(Func<ZoneTreeOptions<TKey, TValue>, ITransactionLog<TKey, TValue>> transactionLogGetter)
    {
        GetTransactionLog = transactionLogGetter;
        return this;
    }

    /// <summary>
    /// Sets initial sparse array length. 
    /// Factory initializes the sparse array with given size when the database is loaded.
    /// </summary>
    /// <param name="initialSparseArrayLength">The initial sparse array length.</param>
    /// <returns>ZoneTree Factory</returns>
    public ZoneTreeFactory<TKey, TValue> SetInitialSparseArrayLength(int initialSparseArrayLength)
    {
        InitialSparseArrayLength = initialSparseArrayLength;
        return this;
    }

    void FillMissingOptionsForKnownTypes()
    {
        if (Options.RandomAccessDeviceManager == null)
        {
            Options.RandomAccessDeviceManager = new RandomAccessDeviceManager(
                Options.Logger,
                FileStreamProvider);
        }
        FillComparer();
        FillKeySerializer();
        FillValueSerializer();
    }

    void FillComparer()
    {
        if (Options.Comparer != null)
            return;
        TKey key = default;
        Options.Comparer = key switch
        {
            byte => new ByteComparerAscending() as IRefComparer<TKey>,
            char => new CharComparerAscending() as IRefComparer<TKey>,
            DateTime => new DateTimeComparerAscending() as IRefComparer<TKey>,
            decimal => new DecimalComparerAscending() as IRefComparer<TKey>,
            double => new DoubleComparerAscending() as IRefComparer<TKey>,
            short => new Int16ComparerAscending() as IRefComparer<TKey>,
            int => new Int32ComparerAscending() as IRefComparer<TKey>,
            long => new Int64ComparerAscending() as IRefComparer<TKey>,
            Guid => new GuidComparerAscending() as IRefComparer<TKey>,
            _ => null
        };
        if (typeof(TKey) == typeof(string))
            Options.Comparer =
                new StringOrdinalComparerAscending() as IRefComparer<TKey>;

        else if (typeof(TKey) == typeof(byte[]))
            Options.Comparer =
                new ByteArrayComparerAscending() as IRefComparer<TKey>;
    }

    void FillKeySerializer()
    {
        if (Options.KeySerializer != null)
            return;
        TKey key = default;
        Options.KeySerializer = key switch
        {
            byte => new ByteSerializer() as ISerializer<TKey>,
            char => new CharSerializer() as ISerializer<TKey>,
            DateTime => new DateTimeSerializer() as ISerializer<TKey>,
            decimal => new DecimalSerializer() as ISerializer<TKey>,
            double => new DoubleSerializer() as ISerializer<TKey>,
            short => new Int16Serializer() as ISerializer<TKey>,
            int => new Int32Serializer() as ISerializer<TKey>,
            long => new Int64Serializer() as ISerializer<TKey>,
            Guid => new StructSerializer<Guid>() as ISerializer<TKey>,
            _ => null
        };

        if (typeof(TKey) == typeof(string))
            Options.KeySerializer = 
                new Utf8StringSerializer() as ISerializer<TKey>;

        else if (typeof(TKey) == typeof(byte[]))
            Options.KeySerializer =
                new ByteArraySerializer() as ISerializer<TKey>;
    }

    void FillValueSerializer()
    {
        if (Options.ValueSerializer != null)
            return;
        TValue value = default;
        Options.ValueSerializer = value switch
        {
            byte => new ByteSerializer() as ISerializer<TValue>,
            bool => new BooleanSerializer() as ISerializer<TValue>,
            char => new CharSerializer() as ISerializer<TValue>,
            DateTime => new DateTimeSerializer() as ISerializer<TValue>,
            decimal => new DecimalSerializer() as ISerializer<TValue>,
            double => new DoubleSerializer() as ISerializer<TValue>,
            short => new Int16Serializer() as ISerializer<TValue>,
            int => new Int32Serializer() as ISerializer<TValue>,
            long => new Int64Serializer() as ISerializer<TValue>,
            Guid => new StructSerializer<Guid>() as ISerializer<TValue>,
            _ => null
        };

        if (typeof(TValue) == typeof(string))
            Options.ValueSerializer =
                new Utf8StringSerializer() as ISerializer<TValue>;

        else if (typeof(TValue) == typeof(byte[]))
            Options.ValueSerializer =
                new ByteArraySerializer() as ISerializer<TValue>;
    }

    /// <summary>
    /// Opens or creates a ZoneTree.
    /// </summary>
    /// <returns>ZoneTree Factory</returns>
    public IZoneTree<TKey, TValue> OpenOrCreate()
    {
        FillMissingOptionsForKnownTypes();
        InitWriteAheadLogProvider();
        var loader = new ZoneTreeLoader<TKey, TValue>(Options);
        if (loader.ZoneTreeMetaExists)
        {
            var zoneTree = loader.LoadZoneTree();
            var t1 = Task.Run(() =>
                zoneTree.Maintenance.DiskSegment.InitSparseArray(InitialSparseArrayLength));
            Parallel.ForEach(zoneTree.Maintenance.BottomSegments, (bs) =>
            {
                bs.InitSparseArray(InitialSparseArrayLength);
            });
            t1.Wait();
            return zoneTree;
        }
        return new ZoneTree<TKey, TValue>(Options);
    }

    /// <summary>
    /// Creates a ZoneTree.
    /// </summary>
    /// <returns>ZoneTree Factory</returns>
    /// <exception cref="DatabaseAlreadyExistsException">Thrown when the database exists in the location.</exception>
    public IZoneTree<TKey, TValue> Create()
    {
        FillMissingOptionsForKnownTypes();
        InitWriteAheadLogProvider();
        var loader = new ZoneTreeLoader<TKey, TValue>(Options);
        if (loader.ZoneTreeMetaExists)
            throw new DatabaseAlreadyExistsException();
        return new ZoneTree<TKey, TValue>(Options);
    }

    /// <summary>
    /// Opens a ZoneTree.
    /// </summary>
    /// <returns>ZoneTree Factory</returns>
    /// <exception cref="DatabaseNotFoundException">Thrown when database is not found in the location.</exception>
    public IZoneTree<TKey, TValue> Open()
    {
        FillMissingOptionsForKnownTypes();
        InitWriteAheadLogProvider();
        var loader = new ZoneTreeLoader<TKey, TValue>(Options);
        if (!loader.ZoneTreeMetaExists)
            throw new DatabaseNotFoundException();
        return loader.LoadZoneTree();
    }

    /// <summary>
    /// Opens or creates a transactional ZoneTree.
    /// </summary>
    /// <returns>ZoneTree Factory</returns>
    public ITransactionalZoneTree<TKey, TValue> OpenOrCreateTransactional()
    {
        var zoneTree = OpenOrCreate(); 
        InitTransactionLog();
        return new OptimisticZoneTree<TKey, TValue>(Options, TransactionLog, zoneTree);
    }

    /// <summary>
    /// Creates a transactional ZoneTree.
    /// </summary>
    /// <returns>ZoneTree Factory</returns>
    /// <exception cref="DatabaseAlreadyExistsException">Thrown when the database exists in the location.</exception>
    public ITransactionalZoneTree<TKey, TValue> CreateTransactional()
    {
        var zoneTree = Create(); 
        InitTransactionLog();
        return new OptimisticZoneTree<TKey, TValue>(Options, TransactionLog, zoneTree);
    }

    /// <summary>
    /// Opens a transactional ZoneTree.
    /// </summary>
    /// <returns>ZoneTree Factory</returns>
    /// <exception cref="DatabaseNotFoundException">Thrown when database is not found in the location.</exception>
    public ITransactionalZoneTree<TKey, TValue> OpenTransactional()
    {
        var zoneTree = Open();
        InitTransactionLog();
        return new OptimisticZoneTree<TKey, TValue>(Options, TransactionLog, zoneTree);
    }
}