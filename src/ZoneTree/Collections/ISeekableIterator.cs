namespace Tenray.ZoneTree.Collections;

public interface ISeekableIterator<TKey, TValue>
{
    bool SeekBegin();

    bool SeekEnd();

    bool SeekToLastSmallerOrEqualElement(in TKey key);

    bool SeekToFirstGreaterOrEqualElement(in TKey key);

    bool Next();

    bool Prev();

    bool HasCurrent { get; }

    TKey CurrentKey { get; }

    TValue CurrentValue { get; }

    void Skip(long offset);

    bool IsBeginningOfAPart { get; }

    bool IsEndOfAPart { get; }

    int GetPartIndex();

    bool IsFullyFrozen { get; }
}