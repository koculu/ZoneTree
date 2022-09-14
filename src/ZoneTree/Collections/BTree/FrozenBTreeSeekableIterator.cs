namespace Tenray.ZoneTree.Collections.BTree;

public sealed class FrozenBTreeSeekableIterator<TKey, TValue>
    : ISeekableIterator<TKey, TValue>
{
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

    public bool IsBeginningOfAPart => false;

    public bool IsEndOfAPart => false;

    public bool IsFullyFrozen => BTree.IsReadOnly;

    readonly BTree<TKey, TValue> BTree;

    BTree<TKey, TValue>.FrozenNodeIterator CurrentNode;

    public FrozenBTreeSeekableIterator(BTree<TKey, TValue> bTree)
    {
        BTree = bTree;
        CurrentNode = bTree.GetFrozenFirstIterator();
    }

    public bool Next()
    {
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
        CurrentNode = BTree.GetFrozenFirstIterator();
        CurrentNode.SeekBegin();
        return HasCurrent;
    }

    public bool SeekEnd()
    {
        CurrentNode = BTree.GetFrozenLastIterator();
        CurrentNode.SeekEnd();
        return HasCurrent;
    }

    public bool SeekToFirstGreaterOrEqualElement(in TKey key)
    {
        CurrentNode = BTree.GetFrozenIteratorWithFirstKeyGreaterOrEqual(in key);
        return HasCurrent;
    }

    public bool SeekToLastSmallerOrEqualElement(in TKey key)
    {
        CurrentNode = BTree.GetFrozenIteratorWithLastKeySmallerOrEqual(in key);
        return HasCurrent;
    }

    public void Skip(long offset)
    {
        throw new NotSupportedException();
    }

    public int GetPartIndex() => -1;
}
