using Tenray.WAL;
using ZoneTree.Core;

namespace ZoneTree.WAL;

public class BasicWriteAheadLogProvider<TKey, TValue> : IWriteAheadLogProvider<TKey, TValue>
{
    readonly Dictionary<string, IWriteAheadLog<TKey, TValue>> WALTable = new();
    
    public ISerializer<TKey> KeySerializer { get; }
    
    public ISerializer<TValue> ValueSerializer { get; }
    
    public string WalDirectory { get; }

    public BasicWriteAheadLogProvider(
        ISerializer<TKey> keySerializer,
        ISerializer<TValue> valueSerializer,
        string walDirectory = "data/wal")
    {
        KeySerializer = keySerializer;
        ValueSerializer = valueSerializer;
        WalDirectory = walDirectory;
        Directory.CreateDirectory(walDirectory);
    }

    public IWriteAheadLog<TKey, TValue> GetOrCreateWAL(int segmentId)
    {
        return GetOrCreateWAL(segmentId, string.Empty);
    }

    public IWriteAheadLog<TKey, TValue> GetOrCreateWAL(int segmentId, string category)
    {
        var walPath = Path.Combine(WalDirectory, category, segmentId + ".wal");
        if (WALTable.TryGetValue(segmentId + category, out var value))
        {
            return value;
        }
        var wal = new FileSystemWriteAheadLog<TKey, TValue>(
            KeySerializer,
            ValueSerializer,
            walPath);
        WALTable.Add(segmentId + category, wal);
        return wal;
    }

    public IWriteAheadLog<TKey, TValue> GetWAL(int segmentId)
    {
        return GetWAL(segmentId, string.Empty);
    }

    public IWriteAheadLog<TKey, TValue> GetWAL(int segmentId, string category)
    {
        if (WALTable.TryGetValue(segmentId + category, out var value))
        {
            return value;
        }
        return null;
    }

    public bool RemoveWAL(int segmentId)
    {
        return RemoveWAL(segmentId, string.Empty);
    }

    public bool RemoveWAL(int segmentId, string category)
    {
        return WALTable.Remove(segmentId + category);
    }

    public void DropStore()
    {
        if (Directory.Exists(WalDirectory))
            Directory.Delete(WalDirectory, true);
    }
}
