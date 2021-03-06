using Tenray.ZoneTree.Collections;

namespace Tenray.ZoneTree.Segments.Disk;

public class NullDiskSegmentSeekableIterator<TKey, TValue> : ISeekableIterator<TKey, TValue>
{
    public TKey CurrentKey => throw new IndexOutOfRangeException("NullDiskSegment is always empty.");

    public TValue CurrentValue =>
        throw new IndexOutOfRangeException("NullDiskSegment is empty.");

    public bool HasCurrent => false;

    public bool Next()
    {
        return false;
    }

    public bool Prev()
    {
        return false;
    }

    public bool SeekBegin()
    {
        return false;
    }

    public bool SeekEnd()
    {
        return false;
    }

    public bool SeekToLastSmallerOrEqualElement(in TKey key)
    {
        return false;
    }

    public bool SeekToFirstGreaterOrEqualElement(in TKey key)
    {
        return false;
    }
}

