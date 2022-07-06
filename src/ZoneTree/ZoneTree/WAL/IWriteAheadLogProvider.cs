using Tenray.WAL;

namespace ZoneTree.WAL;

public interface IWriteAheadLogProvider<TKey, TValue>
{
    IWriteAheadLog<TKey, TValue> GetOrCreateWAL(int segmentId);

    IWriteAheadLog<TKey, TValue> GetWAL(int segmentId);
    
    bool RemoveWAL(int segmentId);
    
    void DropStore();
}
