using Tenray.WAL;
using ZoneTree.Core;

namespace ZoneTree.WAL;

public interface IWriteAheadLogProvider
{
    IWriteAheadLog<TKey, TValue> GetOrCreateWAL<TKey, TValue>(
        int segmentId,
        ISerializer<TKey> keySerializer,
        ISerializer<TValue> valueSerialize);

    IWriteAheadLog<TKey, TValue> GetOrCreateWAL<TKey, TValue>(
        int segmentId,
        string category,
        ISerializer<TKey> keySerializer,
        ISerializer<TValue> valueSerialize);

    IWriteAheadLog<TKey, TValue> GetWAL<TKey, TValue>(int segmentId);

    IWriteAheadLog<TKey, TValue> GetWAL<TKey, TValue>(int segmentId, string category);

    bool RemoveWAL(int segmentId);

    bool RemoveWAL(int segmentId, string category);

    void DropStore();
}
