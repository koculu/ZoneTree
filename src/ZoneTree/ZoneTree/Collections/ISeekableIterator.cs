namespace ZoneTree.Collections;

public interface ISeekableIterator<TKey, TValue>
{
    bool SeekBegin();

    bool SeekEnd();

    bool SeekToLastSmallerOrEqualElement(in TKey key);

    bool SeekToFirstGreaterOrEqualElement(in TKey key);

    bool Next();
    
    bool Prev();

    bool HasCurrent { get; }

    public TKey CurrentKey { get; }

    public TValue CurrentValue { get; }
}


