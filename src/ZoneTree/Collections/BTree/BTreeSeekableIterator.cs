namespace Tenray.ZoneTree.Collections.BTree;

public sealed class BTreeSeekableIterator<TKey, TValue>
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

    BTree<TKey, TValue>.NodeIterator CurrentNode;

    public BTreeSeekableIterator(BTree<TKey, TValue> bTree)
    {
        BTree = bTree;
        CurrentNode = bTree.GetFirstIterator();
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
        prevNode.SeekEnd();
        CurrentNode = prevNode;
        return prevNode.HasCurrent;
    }

    public bool SeekBegin()
    {
        CurrentNode = BTree.GetFirstIterator();
        CurrentNode.SeekBegin();
        return HasCurrent;
    }

    public bool SeekEnd()
    {
        CurrentNode = BTree.GetLastIterator();
        CurrentNode.SeekEnd();
        return HasCurrent;
    }

    public bool SeekToFirstGreaterOrEqualElement(in TKey key)
    {
        CurrentNode = BTree.GetIteratorWithFirstKeyGreaterOrEqual(in key);
        return HasCurrent;
    }

    public bool SeekToLastSmallerOrEqualElement(in TKey key)
    {
        CurrentNode = BTree.GetIteratorWithLastKeySmallerOrEqual(in key);
        return HasCurrent;
    }

    public void Skip(long offset)
    {
        throw new NotSupportedException();
    }

    public int GetPartIndex() => -1;
}
