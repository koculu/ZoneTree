using Tenray.WAL;

namespace ZoneTree.WAL;

public interface IWriteAheadLogProvider<TKey, TValue>
{
    IWriteAheadLog<TKey, TValue> GetOrCreateWAL(int segmentId);

    IWriteAheadLog<TKey, TValue> GetOrCreateWAL(int segmentId, string category);

    IWriteAheadLog<TKey, TValue> GetWAL(int segmentId);

    IWriteAheadLog<TKey, TValue> GetWAL(int segmentId, string category);

    bool RemoveWAL(int segmentId);

    bool RemoveWAL(int segmentId, string category);

    void DropStore();
}
