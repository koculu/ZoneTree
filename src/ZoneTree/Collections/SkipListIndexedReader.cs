namespace ZoneTree.Collections;

public class SkipListIndexedReader<TKey, TValue> : IIndexedReader<TKey, TValue>
{
    public int Length => SkipList.Length;

    public int Position { get; private set; }

    readonly SkipList<TKey, TValue> SkipList;

    SkipList<TKey, TValue>.SkipListNode CurrentNode;

    public SkipListIndexedReader(SkipList<TKey, TValue> skipList)
    {
        SkipList = skipList;
        CurrentNode = skipList.FirstNode;
    }

    public TKey GetKey(int index)
    {
        var pos = Position;
        var node = CurrentNode;
        while (index > pos)
        {
            if (node == null)
                break;
            while (!node.isInserted); //spin lock
            node = node.NextNode;
            ++pos;
        }

        while (index < pos)
        {
            if (node == null)
                break;
            while (!node.isInserted); //spin lock
            node = node.PreviousNode;
            --pos;
        }

        if (node == null)
            throw new IndexOutOfRangeException($"index: {index} is out of range.");

        Position = pos;
        CurrentNode = node;
        return node.Key;
    }

    public TValue GetValue(int index)
    {
        var pos = Position;
        var node = CurrentNode;
        while (index > pos)
        {
            if (node == null)
                break;
            while (!node.isInserted); //spin lock
            node = node.NextNode;
            ++pos;
        }

        while (index < pos)
        {
            if (node == null)
                break;
            while (!node.isInserted); //spin lock
            node = node.PreviousNode;
            --pos;
        }

        if (node == null)
            throw new IndexOutOfRangeException($"index: {index} is out of range.");

        Position = pos;
        CurrentNode = node;
        return node.Value;
    }
    
    public void SeekBegin()
    {
        CurrentNode = SkipList.FirstNode;
        Position = 0;
    }

    public void SeekEnd()
    {
        CurrentNode = SkipList.LastNode;
        Position = SkipList.Length - 1;
    }

    public int GetLastSmallerOrEqualPosition(in TKey key)
    {
        throw new NotSupportedException("SkipListIndexedReader does not support lower or equal bound.");
    }

    public int GetFirstGreaterOrEqualPosition(in TKey key)
    {
        throw new NotSupportedException("SkipListIndexedReader does not support lower or equal bound.");
    }
}

