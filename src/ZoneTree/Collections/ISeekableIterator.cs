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

    void Skip(int offset);

    bool IsBeginningOfASector { get; }

    bool IsEndOfASector { get; }

    int GetSectorIndex();

    bool IsFullyFrozen { get; }
}