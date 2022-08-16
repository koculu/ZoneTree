using System.Collections.Concurrent;
using Tenray.ZoneTree.AbstractFileStream;
using Tenray.ZoneTree.Core;

namespace Tenray.ZoneTree.WAL;

public class BasicWriteAheadLogProvider : IWriteAheadLogProvider
{
    readonly IFileStreamProvider FileStreamProvider;

    readonly ConcurrentDictionary<string, object> WALTable = new();

    public string WalDirectory { get; }

    public WriteAheadLogMode WriteAheadLogMode { get; set; } 
        = WriteAheadLogMode.Lazy;

    public int CompressionBlockSize { get; set; } = 1024 * 32 * 8;

    public bool EnableIncrementalBackup { get; set; }

    public CompressedImmediateModeOptions CompressedImmediateModeOptions { get; } = new();

    public LazyModeOptions LazyModeOptions { get; } = new();

    public BasicWriteAheadLogProvider(
        IFileStreamProvider fileStreamProvider,
        string walDirectory = "data")
    {
        FileStreamProvider = fileStreamProvider;
        WalDirectory = walDirectory;
        FileStreamProvider.CreateDirectory(walDirectory);
    }

    public IWriteAheadLog<TKey, TValue> GetOrCreateWAL<TKey, TValue>(
        long segmentId,
        string category,
        ISerializer<TKey> keySerializer,
        ISerializer<TValue> valueSerializer)
    {
        var walPath = Path.Combine(WalDirectory, category, segmentId + ".wal");
        if (WALTable.TryGetValue(segmentId + category, out var value))
        {
            return (IWriteAheadLog<TKey, TValue>)value;
        }

        switch (WriteAheadLogMode)
        {
            case WriteAheadLogMode.None:
                return new NullWriteAheadLog<TKey, TValue>();
            case WriteAheadLogMode.Immediate:
                {
                    var wal = new FileSystemWriteAheadLog<TKey, TValue>(
                        FileStreamProvider,
                        keySerializer,
                        valueSerializer,
                        walPath)
                    {
                        EnableIncrementalBackup = EnableIncrementalBackup
                    };
                    WALTable.TryAdd(segmentId + category, wal);
                    return wal;
                }
            case WriteAheadLogMode.CompressedImmediate:
                {
                    var wal = new CompressedFileSystemWriteAheadLog<TKey, TValue>(
                        FileStreamProvider,
                        keySerializer,
                        valueSerializer,
                        walPath,
                        CompressionBlockSize,
                        CompressedImmediateModeOptions.EnableTailWriterJob,
                        CompressedImmediateModeOptions.TailWriterJobInterval)
                    {
                        EnableIncrementalBackup = EnableIncrementalBackup
                    };
                    WALTable.TryAdd(segmentId + category, wal);
                    return wal;
                }

            case WriteAheadLogMode.Lazy:
                {
                    var wal = new LazyFileSystemWriteAheadLog<TKey, TValue>(
                        FileStreamProvider,
                        keySerializer,
                        valueSerializer,
                        walPath,
                        CompressionBlockSize,
                        LazyModeOptions.EmptyQueuePollInterval)
                    {
                        EnableIncrementalBackup = EnableIncrementalBackup
                    };
                    WALTable.TryAdd(segmentId + category, wal);
                    return wal;
                }
        }
        return null;
    }

    public IWriteAheadLog<TKey, TValue> GetWAL<TKey, TValue>(long segmentId, string category)
    {
        if (WALTable.TryGetValue(segmentId + category, out var value))
        {
            return (IWriteAheadLog<TKey, TValue>)value;
        }
        return null;
    }

    public bool RemoveWAL(long segmentId, string category)
    {
        return WALTable.Remove(segmentId + category, out _);
    }

    public void DropStore()
    {
        if (FileStreamProvider.DirectoryExists(WalDirectory))
            FileStreamProvider.DeleteDirectory(WalDirectory, true);
    }

    public void InitCategory(string category)
    {
        var categoryPath = Path.Combine(WalDirectory, category);
        FileStreamProvider.CreateDirectory(categoryPath);
    }
}
