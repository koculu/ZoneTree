using Tenray.WAL;

namespace ZoneTree.WAL;

public class NullWriteAheadLogProvider<TKey, TValue> : IWriteAheadLogProvider<TKey, TValue>
{
    public IWriteAheadLog<TKey, TValue> GetOrCreateWAL(int segmentId)
    {
        return new NullWriteAheadLog<TKey, TValue>();
    }

    public IWriteAheadLog<TKey, TValue> GetWAL(int segmentId)
    {
        return new NullWriteAheadLog<TKey, TValue>();
    }

    public bool RemoveWAL(int segmentId)
    {
        return false;
    }

    public void DropStore()
    {
        // Nothing to drop
    }

    public IWriteAheadLog<TKey, TValue> GetOrCreateWAL(int segmentId, string category)
    {
        return new NullWriteAheadLog<TKey, TValue>();
    }

    public IWriteAheadLog<TKey, TValue> GetWAL(int segmentId, string category)
    {
        return new NullWriteAheadLog<TKey, TValue>();
    }

    public bool RemoveWAL(int segmentId, string category)
    {
        return false;
    }
}