using Tenray.ZoneTree.Options;
using Tenray.ZoneTree.Serializers;

namespace Tenray.ZoneTree.WAL;

public sealed class NullWriteAheadLogProvider : IWriteAheadLogProvider
{
    public IWriteAheadLog<TKey, TValue> GetOrCreateWAL<TKey, TValue>(
        long segmentId, 
        string category,
        WriteAheadLogOptions options,
        ISerializer<TKey> keySerializer,
        ISerializer<TValue> valueSerialize)
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