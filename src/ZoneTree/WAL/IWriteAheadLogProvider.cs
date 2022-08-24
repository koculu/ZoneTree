using Tenray.ZoneTree.Core;
using Tenray.ZoneTree.Options;

namespace Tenray.ZoneTree.WAL;

public interface IWriteAheadLogProvider
{
    void InitCategory(string category);

    IWriteAheadLog<TKey, TValue> GetOrCreateWAL<TKey, TValue>(
        long segmentId,
        string category,
        WriteAheadLogOptions options,
        ISerializer<TKey> keySerializer,
        ISerializer<TValue> valueSerialize);

    IWriteAheadLog<TKey, TValue> GetWAL<TKey, TValue>(long segmentId, string category);

    bool RemoveWAL(long segmentId, string category);

    void DropStore();
}
