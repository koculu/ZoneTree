using Tenray.WAL;
using ZoneTree.Core;

namespace ZoneTree.WAL;

public class NullWriteAheadLogProvider : IWriteAheadLogProvider
{
    public IWriteAheadLog<TKey, TValue> GetOrCreateWAL<TKey, TValue>(long segmentId, ISerializer<TKey> keySerializer, ISerializer<TValue> valueSerialize)
    {
        return new NullWriteAheadLog<TKey, TValue>();
    }

    public IWriteAheadLog<TKey, TValue> GetOrCreateWAL<TKey, TValue>(long segmentId, string category, ISerializer<TKey> keySerializer, ISerializer<TValue> valueSerialize)
    {
        return new NullWriteAheadLog<TKey, TValue>();
    }

    public IWriteAheadLog<TKey, TValue> GetWAL<TKey, TValue>(long segmentId)
    {
        return new NullWriteAheadLog<TKey, TValue>();
    }

    public IWriteAheadLog<TKey, TValue> GetWAL<TKey, TValue>(long segmentId, string category)
    {
        return new NullWriteAheadLog<TKey, TValue>();
    }

    public bool RemoveWAL(long segmentId)
    {
        return false;
    }

    public bool RemoveWAL(long segmentId, string category)
    {
        return false;
    }

    public void DropStore()
    {
        // Nothing to drop
    }
}