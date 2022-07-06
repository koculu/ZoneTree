namespace ZoneTree.Segments.Disk;

public interface IDiskSegmentCreator<TKey, TValue> : IDisposable
{
    void Append(TKey key, TValue value);

    IDiskSegment<TKey, TValue> CreateReadOnlyDiskSegment();
}
