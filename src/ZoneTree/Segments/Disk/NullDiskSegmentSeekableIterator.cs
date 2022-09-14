using Tenray.ZoneTree.Collections;

namespace Tenray.ZoneTree.Segments.Disk;

public sealed class NullDiskSegmentSeekableIterator<TKey, TValue> : ISeekableIterator<TKey, TValue>
{
    public TKey CurrentKey => throw new IndexOutOfRangeException("NullDiskSegment is always empty.");

    public TValue CurrentValue =>
        throw new IndexOutOfRangeException("NullDiskSegment is empty.");

    public bool HasCurrent => false;

    public bool IsBeginningOfAPart => false;

    public bool IsEndOfAPart => false;

    public bool IsFullyFrozen => true;

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

    public void Skip(long offset)
    {
        throw new NotSupportedException();
    }

    public int GetPartIndex() => -1;
}

