namespace Tenray.ZoneTree.Collections.BplusTree;

/// <summary>
/// In memory B+Tree.
/// This class is thread-safe.
/// </summary>
/// <typeparam name="TKey">Key Type</typeparam>
/// <typeparam name="TValue">Value Type</typeparam>
public partial class SafeBplusTree<TKey, TValue>
{
    readonly NoLock TopLevelLocker = new();

    readonly int NodeSize = 128;

    readonly int LeafSize = 128;

    volatile Node Root;

    readonly LeafNode FirstLeafNode;

    volatile LeafNode LastLeafNode;

    public readonly IRefComparer<TKey> Comparer;

    volatile int _length;

    public int Length => _length;

    public SafeBplusTree(
        IRefComparer<TKey> comparer,
        int nodeSize = 128,
        int leafSize = 128)
    {
        NodeSize = nodeSize;
        LeafSize = leafSize;
        Comparer = comparer;
        Root = new LeafNode(LeafSize);
        FirstLeafNode = Root as LeafNode;
        LastLeafNode = FirstLeafNode;
    }

    public void Lock()
    {
        TopLevelLocker.Lock();
    }

    public void Unlock()
    {
        TopLevelLocker.Unlock();
    }

    public void ReadLock()
    {
        TopLevelLocker.SharedLock();
    }

    public void ReadUnlock()
    {
        TopLevelLocker.SharedUnlock();
    }

    public NodeIterator GetIteratorWithLastKeySmallerOrEqual(in TKey key)
    {
        try
        {
            ReadLock();
            var iterator = GetLeafNode(key).GetIterator(this);
            return iterator.SeekLastKeySmallerOrEqual(Comparer, in key);
        }
        finally
        {
            ReadUnlock();
        }
    }

    public NodeIterator GetIteratorWithFirstKeyGreaterOrEqual(in TKey key)
    {
        try
        {
            ReadLock();
            var iterator = GetLeafNode(key).GetIterator(this);
            return iterator.SeekFirstKeyGreaterOrEqual(Comparer, in key);
        }
        finally
        {
            ReadUnlock();
        }
    }

    public FrozenNodeIterator GetFrozenIteratorWithLastKeySmallerOrEqual(in TKey key)
    {
        var iterator = GetFrozenLeafNode(key).GetFrozenIterator();
        return iterator.SeekLastKeySmallerOrEqual(Comparer, in key);
    }

    public FrozenNodeIterator GetFrozenIteratorWithFirstKeyGreaterOrEqual(in TKey key)
    {
        var iterator = GetFrozenLeafNode(key).GetFrozenIterator();
        return iterator.SeekFirstKeyGreaterOrEqual(Comparer, in key);
    }
        
    public NodeIterator GetFirstIterator()
    {
        try
        {
            ReadLock();
            return FirstLeafNode.GetIterator(this);
        }
        finally
        {
            ReadUnlock();
        }
    }

    public FrozenNodeIterator GetFrozenFirstIterator()
    {
        return FirstLeafNode.GetFrozenIterator();
    }

    public NodeIterator GetLastIterator()
    {
        try
        {
            ReadLock();
            return LastLeafNode.GetIterator(this);
        }
        finally
        {
            ReadUnlock();
        }
    }

    public FrozenNodeIterator GetFrozenLastIterator()
    {
        return LastLeafNode.GetFrozenIterator();
    }
       
}