using Tenray.ZoneTree.Core;

namespace Tenray.ZoneTree.WAL;

public class BasicWriteAheadLogProvider : IWriteAheadLogProvider
{
    readonly Dictionary<string, object> WALTable = new();

    public string WalDirectory { get; }

    public WriteAheadLogMode WriteAheadLogMode { get; set; }

    public BasicWriteAheadLogProvider(string walDirectory = "data")
    {
        WalDirectory = walDirectory;
        Directory.CreateDirectory(walDirectory);
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
        var wal = new FileSystemWriteAheadLog<TKey, TValue>(
            keySerializer,
            valueSerializer,
            walPath);
        WALTable.Add(segmentId + category, wal);
        return wal;
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
        return WALTable.Remove(segmentId + category);
    }

    public void DropStore()
    {
        if (Directory.Exists(WalDirectory))
            Directory.Delete(WalDirectory, true);
    }

    public void InitCategory(string category)
    {
        var categoryPath = Path.Combine(WalDirectory, category);
        Directory.CreateDirectory(categoryPath);
    }
}
