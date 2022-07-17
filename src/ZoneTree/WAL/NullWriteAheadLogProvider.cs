using Tenray.WAL;
using ZoneTree.Core;

namespace ZoneTree.WAL;

public class NullWriteAheadLogProvider : IWriteAheadLogProvider
{
    public IWriteAheadLog<TKey, TValue> GetOrCreateWAL<TKey, TValue>(int segmentId, ISerializer<TKey> keySerializer, ISerializer<TValue> valueSerialize)
    {
        return new NullWriteAheadLog<TKey, TValue>();
    }

    public IWriteAheadLog<TKey, TValue> GetOrCreateWAL<TKey, TValue>(int segmentId, string category, ISerializer<TKey> keySerializer, ISerializer<TValue> valueSerialize)
    {
        return new NullWriteAheadLog<TKey, TValue>();
    }

    public IWriteAheadLog<TKey, TValue> GetWAL<TKey, TValue>(int segmentId)
    {
        return new NullWriteAheadLog<TKey, TValue>();
    }

    public IWriteAheadLog<TKey, TValue> GetWAL<TKey, TValue>(int segmentId, string category)
    {
        return new NullWriteAheadLog<TKey, TValue>();
    }

    public bool RemoveWAL(int segmentId)
    {
        return false;
    }

    public bool RemoveWAL(int segmentId, string category)
    {
        return false;
    }

    public void DropStore()
    {
        // Nothing to drop
    }
}