using Tenray.ZoneTree.Collections;

namespace Tenray.ZoneTree.Segments;

public interface IReadOnlySegment<TKey, TValue>
{
    long SegmentId { get; }

    long Length { get; }

    long MaximumOpIndex { get; }

    bool ContainsKey(in TKey key);

    bool TryGet(in TKey key, out TValue value);

    void Drop();

    void ReleaseResources();

    IIndexedReader<TKey, TValue> GetIndexedReader();

    ISeekableIterator<TKey, TValue> GetSeekableIterator();

    /// <summary>
    /// This flag indicates that the readonly segment has completed all writes
    /// and is guaranteed to be frozen.
    /// </summary>
    bool IsFullyFrozen { get; }
}