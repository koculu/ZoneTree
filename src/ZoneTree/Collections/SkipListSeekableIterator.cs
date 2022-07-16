namespace ZoneTree.Collections;

public class SkipListSeekableIterator<TKey, TValue> : ISeekableIterator<TKey, TValue>
{
    public TKey CurrentKey =>
        CurrentNode == null ?
        throw new IndexOutOfRangeException("Iterator is not in a valid position. Have you forgotten to call Next() or Prev()?") :
        CurrentNode.Key;

    public TValue CurrentValue =>
        CurrentNode == null ?
        throw new IndexOutOfRangeException("Iterator is not in a valid position. Did you forget to call Next() or Prev()?") :
        CurrentNode.Value;

    public bool HasCurrent => CurrentNode != null;

    readonly SkipList<TKey, TValue> SkipList;

    SkipList<TKey, TValue>.SkipListNode CurrentNode;

    SkipList<TKey, TValue>.SkipListNode NextNode;

    SkipList<TKey, TValue>.SkipListNode PreviousNode;

    public SkipListSeekableIterator(SkipList<TKey, TValue> skipList)
    {
        SkipList = skipList;
        NextNode = skipList.FirstNode;
    }

    public bool Next()
    {
        var node = NextNode;
        if (node == null)
            return false;

        CurrentNode = node;
        AdjustLinks();

        return true;
    }

    public bool Prev()
    {
        var node = PreviousNode;
        if (node == null)
            return false;

        CurrentNode = node;
        AdjustLinks();
        return true;
    }

    void AdjustLinks()
    {
        var node = CurrentNode;
        if (node == null)
        {
            CurrentNode = null;
            PreviousNode = null;
            NextNode = null;
            return;
        }
        node.EnsureNodeIsInserted();
        PreviousNode = node.GetPrevious();
        NextNode = node.NextNode;
    }

    public bool SeekBegin()
    {
        var node = SkipList.FirstNode;
        CurrentNode = node;
        AdjustLinks();
        return HasCurrent;
    }

    public bool SeekEnd()
    {
        var node = SkipList.LastNode;
        CurrentNode = node;
        AdjustLinks();
        return HasCurrent;
    }

    public bool SeekToFirstGreaterOrEqualElement(in TKey key)
    {
        CurrentNode = SkipList.GetFirstNodeGreaterOrEqual(key);
        AdjustLinks();
        return HasCurrent;
    }

    public bool SeekToLastSmallerOrEqualElement(in TKey key)
    {
        CurrentNode = SkipList.GetLastNodeSmallerOrEqual(key);
        AdjustLinks();
        return HasCurrent;
    }
}

