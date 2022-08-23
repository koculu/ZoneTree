using Tenray.ZoneTree.Core;

namespace Tenray.ZoneTree.Segments.Disk;

public interface IDiskSegmentCreator<TKey, TValue> : IDisposable
{
    bool CanSkipCurrentPart { get; }

    HashSet<long> AppendedPartSegmentIds { get; }

    void Append(TKey key, TValue value, IteratorPosition iteratorPosition);
    
    void Append(
        IDiskSegment<TKey, TValue> part,
        TKey key1,
        TKey key2,
        TValue value1,
        TValue value2);

    IDiskSegment<TKey, TValue> CreateReadOnlyDiskSegment();

    void DropDiskSegment();

}
