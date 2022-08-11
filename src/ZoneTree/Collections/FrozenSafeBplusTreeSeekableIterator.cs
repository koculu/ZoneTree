namespace Tenray.ZoneTree.Collections;

public class FrozenSafeBplusTreeSeekableIterator<TKey, TValue>
    : ISeekableIterator<TKey, TValue>
{
    TKey CurrentKeyOrDefault = default;

    public TKey CurrentKey =>
        HasCurrent ?
        CurrentNode.CurrentKey :
        throw new IndexOutOfRangeException(
            "Iterator is not in a valid position. Have you forgotten to call Next() or Prev()?");
        

    public TValue CurrentValue =>
        HasCurrent ?
        CurrentNode.CurrentValue :
        throw new IndexOutOfRangeException(
            "Iterator is not in a valid position. Did you forget to call Next() or Prev()?");        

    public bool HasCurrent => CurrentNode != null && CurrentNode.HasCurrent;

    public bool IsBeginningOfASector => false;

    public bool IsEndOfASector => false;

    readonly SafeBplusTree<TKey, TValue> BplusTree;

    SafeBplusTree<TKey, TValue>.FrozenNodeIterator CurrentNode;

    public FrozenSafeBplusTreeSeekableIterator(SafeBplusTree<TKey, TValue> bplusTree)
    {
        BplusTree = bplusTree;
        CurrentNode = bplusTree.GetFrozenFirstIterator();
    }

    public bool Next()
    {
        if (HasCurrent)
            CurrentKeyOrDefault = CurrentNode.CurrentKey;
        if (CurrentNode.Next())
            return true;

        var nextNode = CurrentNode.GetNextNodeIterator();
        if (nextNode == null)
            return false;
        nextNode.SeekBegin();
        CurrentNode = nextNode;
        return nextNode.HasCurrent;
    }

    public bool Prev()
    {
        if (HasCurrent)
            CurrentKeyOrDefault = CurrentNode.CurrentKey;
        if (CurrentNode.Previous())
            return true;

        var prevNode = CurrentNode.GetPreviousNodeIterator();
        if (prevNode == null)
            return false;
        CurrentNode = prevNode;
        prevNode.SeekEnd();
        return prevNode.HasCurrent;
    }

    public bool SeekBegin()
    {
        CurrentNode = BplusTree.GetFrozenFirstIterator();
        CurrentNode.SeekBegin();
        return HasCurrent;
    }

    public bool SeekEnd()
    {
        CurrentNode = BplusTree.GetFrozenLastIterator();
        CurrentNode.SeekEnd();
        return HasCurrent;
    }

    public bool SeekToFirstGreaterOrEqualElement(in TKey key)
    {
        CurrentNode = BplusTree.GetFrozenIteratorWithFirstKeyGreaterOrEqual(in key);
        return HasCurrent;
    }

    public bool SeekToLastSmallerOrEqualElement(in TKey key)
    {
        CurrentNode = BplusTree.GetFrozenIteratorWithLastKeySmallerOrEqual(in key);
        return HasCurrent;
    }

    public void Skip(int offset)
    {
        throw new NotSupportedException();
    }

    public int GetSectorIndex() => -1;
}
