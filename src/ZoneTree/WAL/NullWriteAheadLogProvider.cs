using Tenray.ZoneTree.Core;

namespace Tenray.ZoneTree.WAL;

public class NullWriteAheadLogProvider : IWriteAheadLogProvider
{
    public WriteAheadLogMode WriteAheadLogMode {get; set;}

    public bool EnableIncrementalBackup { get; set; }

    public CompressedImmediateModeOptions CompressedImmediateModeOptions { get; } = new();

    public LazyModeOptions LazyModeOptions { get; } = new();

    public int CompressionBlockSize { get; set; }

    public IWriteAheadLog<TKey, TValue> GetOrCreateWAL<TKey, TValue>(long segmentId, string category, ISerializer<TKey> keySerializer, ISerializer<TValue> valueSerialize)
    {
        return new NullWriteAheadLog<TKey, TValue>();
    }

    public IWriteAheadLog<TKey, TValue> GetWAL<TKey, TValue>(long segmentId, string category)
    {
        return new NullWriteAheadLog<TKey, TValue>();
    }

    public bool RemoveWAL(long segmentId, string category)
    {
        return false;
    }

    public void DropStore()
    {
        // Nothing to drop
    }

    public void InitCategory(string category)
    {
        // Nothing to init
    }
}