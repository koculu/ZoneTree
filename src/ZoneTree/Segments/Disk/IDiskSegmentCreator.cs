using Tenray.ZoneTree.Core;

namespace Tenray.ZoneTree.Segments.Disk;

public interface IDiskSegmentCreator<TKey, TValue> : IDisposable
{
    bool CanSkipCurrentSector { get; }

    HashSet<int> AppendedSectorSegmentIds { get; }

    void Append(TKey key, TValue value, IteratorPosition iteratorPosition);
    
    void Append(
        IDiskSegment<TKey, TValue> sector,
        TKey key1,
        TKey key2,
        TValue value1,
        TValue value2);

    IDiskSegment<TKey, TValue> CreateReadOnlyDiskSegment();

    void DropDiskSegment();

}
