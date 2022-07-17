using Tenray.WAL;
using ZoneTree.Core;

namespace ZoneTree.WAL;

public class BasicWriteAheadLogProvider : IWriteAheadLogProvider
{
    readonly Dictionary<string, object> WALTable = new();
    
    public string WalDirectory { get; }

    public BasicWriteAheadLogProvider(string walDirectory = "data/wal")
    {
        WalDirectory = walDirectory;
        Directory.CreateDirectory(walDirectory);
    }

    public IWriteAheadLog<TKey, TValue> GetOrCreateWAL<TKey, TValue>(
        int segmentId,
        ISerializer<TKey> keySerializer,
        ISerializer<TValue> valueSerializer)
    {
        return GetOrCreateWAL(segmentId, string.Empty, keySerializer, valueSerializer);
    }

    public IWriteAheadLog<TKey, TValue> GetOrCreateWAL<TKey, TValue>(
        int segmentId,
        string category,
        ISerializer<TKey> keySerializer,
        ISerializer<TValue> valueSerializer)
    {
        var walPath = Path.Combine(WalDirectory, category, segmentId + ".wal");
        if (WALTable.TryGetValue(segmentId + category, out var value))
        {
            return (IWriteAheadLog<TKey, TValue>) value;
        }
        var wal = new FileSystemWriteAheadLog<TKey, TValue>(
            keySerializer,
            valueSerializer,
            walPath);
        WALTable.Add(segmentId + category, wal);
        return wal;
    }

    public IWriteAheadLog<TKey, TValue> GetWAL<TKey, TValue>(int segmentId)
    {
        return GetWAL<TKey, TValue>(segmentId, string.Empty);
    }

    public IWriteAheadLog<TKey, TValue> GetWAL<TKey, TValue>(int segmentId, string category)
    {
        if (WALTable.TryGetValue(segmentId + category, out var value))
        {
            return (IWriteAheadLog<TKey, TValue>) value;
        }
        return null;
    }

    public bool RemoveWAL<TKey, TValue>(int segmentId)
    {
        return RemoveWAL<TKey, TValue>(segmentId, string.Empty);
    }

    public bool RemoveWAL<TKey, TValue>(int segmentId, string category)
    {
        return WALTable.Remove(segmentId + category);
    }

    public void DropStore()
    {
        if (Directory.Exists(WalDirectory))
            Directory.Delete(WalDirectory, true);
    }
}
