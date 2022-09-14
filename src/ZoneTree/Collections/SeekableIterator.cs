namespace Tenray.ZoneTree.Collections;

public sealed class SeekableIterator<TKey, TValue> : ISeekableIterator<TKey, TValue>
{
    readonly IIndexedReader<TKey, TValue> IndexedReader;

    readonly long Length;

    long position = -1;

    public TKey CurrentKey =>
        position == -1 || position >= Length ?
        throw new IndexOutOfRangeException("Iterator is not in a valid position. Have you forgotten to call Next() or Prev()?") :
        IndexedReader.GetKey(position);

    public TValue CurrentValue =>
        position == -1 || position >= Length ?
        throw new IndexOutOfRangeException("Iterator is not in a valid position. Have you forgotten to call Next() or Prev()?") :
        IndexedReader.GetValue(position);

    public bool HasCurrent => position >= 0 && position < Length;

    public bool IsBeginningOfAPart => IndexedReader.IsBeginningOfAPart(position);

    public bool IsEndOfAPart => IndexedReader.IsEndOfAPart(position);

    /// <summary>
    /// All Indexed Readers are always fully frozen.
    /// </summary>
    public bool IsFullyFrozen => true;

    public SeekableIterator(IIndexedReader<TKey, TValue> indexedReader)
    {
        IndexedReader = indexedReader;
        // Pin the length of the indexed reader to improve performance.
        // This seekable iterator is only used by immutable indexed readers.
        // Hence it is safe to pin the length here.
        Length = indexedReader.Length;
    }

    public bool Next()
    {
        if (position >= Length - 1)
            return false;
        ++position;
        return true;
    }

    public bool Prev()
    {
        if (position < 1)
            return false;
        --position;
        return true;
    }

    public bool SeekBegin()
    {
        position = 0;
        return HasCurrent;
    }

    public bool SeekEnd()
    {
        position = Length - 1;
        return HasCurrent;
    }

    public bool SeekToLastSmallerOrEqualElement(in TKey key)
    {
        position = IndexedReader.GetLastSmallerOrEqualPosition(key);
        return HasCurrent;
    }

    public bool SeekToFirstGreaterOrEqualElement(in TKey key)
    {
        position = IndexedReader.GetFirstGreaterOrEqualPosition(key);
        return HasCurrent;
    }

    public void Skip(long offset)
    {
        position += offset;

    }
    public int GetPartIndex() => IndexedReader.GetPartIndex(position);
}

