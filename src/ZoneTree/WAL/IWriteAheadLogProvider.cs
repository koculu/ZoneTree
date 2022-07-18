using Tenray.WAL;
using ZoneTree.Core;

namespace ZoneTree.WAL;

public interface IWriteAheadLogProvider
{
    void InitCategory(string category);

    IWriteAheadLog<TKey, TValue> GetOrCreateWAL<TKey, TValue>(
        long segmentId,
        ISerializer<TKey> keySerializer,
        ISerializer<TValue> valueSerialize);

    IWriteAheadLog<TKey, TValue> GetOrCreateWAL<TKey, TValue>(
        long segmentId,
        string category,
        ISerializer<TKey> keySerializer,
        ISerializer<TValue> valueSerialize);

    IWriteAheadLog<TKey, TValue> GetWAL<TKey, TValue>(long segmentId);

    IWriteAheadLog<TKey, TValue> GetWAL<TKey, TValue>(long segmentId, string category);

    bool RemoveWAL(long segmentId);

    bool RemoveWAL(long segmentId, string category);

    void DropStore();
}
