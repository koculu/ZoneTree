namespace Tenray.ZoneTree.Collections.BTree;

/// <summary>
/// In memory B+Tree.
/// This class is thread-safe.
/// </summary>
/// <typeparam name="TKey">Key Type</typeparam>
/// <typeparam name="TValue">Value Type</typeparam>
public partial class BTree<TKey, TValue>
{
    public bool Upsert(in TKey key, in TValue value)
    {
        try
        {
            WriteLock();
            while (true)
            {
                var root = Root;
                root.WriteLock();
                if (root != Root)
                {
                    root.WriteUnlock();
                    continue;
                }

                if (!root.IsFull)
                {
                    return UpsertNonFull(root, in key, in value);
                }
                var newRoot = new Node(GetNodeLocker(), NodeSize);
                newRoot.Children[0] = root;
                newRoot.WriteLock();
                SplitChild(newRoot, 0, root);
                var result = UpsertNonFull(newRoot, in key, in value);
                Root = newRoot;
                root.WriteUnlock();
                return result;
            }
        }
        finally
        {
            WriteUnlock();
        }
    }

    public bool TryInsert(in TKey key, in TValue value)
    {
        try
        {
            WriteLock();
            while (true)
            {
                var root = Root;
                root.WriteLock();
                if (root != Root)
                {
                    root.WriteUnlock();
                    continue;
                }
                if (!root.IsFull)
                {
                    return TryInsertNonFull(root, in key, in value);
                }
                var newRoot = new Node(GetNodeLocker(), NodeSize);
                newRoot.Children[0] = root;
                newRoot.WriteLock();
                SplitChild(newRoot, 0, root);
                var result = TryInsertNonFull(newRoot, in key, in value);
                Root = newRoot;
                root.WriteUnlock();
                return result;
            }
        }
        catch(Exception e)
        {
            Console.WriteLine(e.ToString());
            Root.WriteUnlock();
            throw;
        }
        finally
        {
            WriteUnlock();
        }
    }

    public delegate AddOrUpdateResult AddDelegate(ref TValue value);

    public delegate AddOrUpdateResult UpdateDelegate(ref TValue value);

    public AddOrUpdateResult AddOrUpdate(
        in TKey key,
        AddDelegate adder,
        UpdateDelegate updater)
    {
        try
        {
            WriteLock();
            while (true)
            {
                var root = Root;
                root.WriteLock();
                if (root != Root)
                {
                    root.WriteUnlock();
                    continue;
                }
                if (!root.IsFull)
                {
                    return TryAddOrUpdateNonFull(root, in key, adder, updater);
                }
                var newRoot = new Node(GetNodeLocker(), NodeSize);
                newRoot.Children[0] = root;
                newRoot.WriteLock();
                SplitChild(newRoot, 0, root);
                AddOrUpdateResult result =
                    TryAddOrUpdateNonFull(newRoot, in key, adder, updater);
                Root = newRoot;
                root.WriteUnlock();
                return result;
            }
        }
        finally
        {
            WriteUnlock();
        }
    }

    void SplitChild(Node parent, int rightChildPosition, Node leftNode)
    {
        var pivotPosition = (leftNode.Length + 1) / 2;
        // tree is locked, safe to read from any node.
        ref var pivotKey = ref leftNode.Keys[pivotPosition];
        if (leftNode is LeafNode leftLeaf)
        {
            var rightLeaf = new LeafNode(GetNodeLocker(), LeafSize);
            var leftNext = leftLeaf.Next;
            if (leftNext == null)
            {
                parent.InsertKeyAndChild(rightChildPosition, in pivotKey, rightLeaf);
                rightLeaf.ReplaceFrom(leftLeaf, pivotPosition);
                if (LastLeafNode == leftLeaf)
                    LastLeafNode = rightLeaf;
            }
            else
            {
                // leftNext is not null. its previous pointer will change.
                // should we do something for iterators?
                parent.InsertKeyAndChild(rightChildPosition, in pivotKey, rightLeaf);
                rightLeaf.ReplaceFrom(leftLeaf, pivotPosition);
                if (LastLeafNode == leftLeaf)
                    LastLeafNode = rightLeaf;
            }
        }
        else
        {
            var rightNode = new Node(GetNodeLocker(), NodeSize);
            parent.InsertKeyAndChild(rightChildPosition, in pivotKey, rightNode);
            rightNode.ReplaceFrom(leftNode, pivotPosition);
        }
    }

    bool UpsertNonFull(Node node, in TKey key, in TValue value)
    {
        while (true)
        {
            var found = node.TryGetPosition(Comparer, in key, out var position);
            if (node is LeafNode leaf)
            {
                if (found)
                {
                    leaf.Update(position, in key, in value);
                    node.WriteUnlock();
                    return false;
                }
                leaf.Insert(position, in key, in value);
                node.WriteUnlock();
                Interlocked.Increment(ref _length);
                return true;
            }

            var child = node.Children[position];
            child.WriteLock();
            if (child.IsFull)
            {
                SplitChild(node, position, child);
                if (Comparer.Compare(in key, in node.Keys[position]) > 0)
                {
                    child.WriteUnlock();
                    child = node.Children[position + 1];
                    if (child.IsFull)
                        throw new Exception("child was full");
                    child.WriteLock();
                }
            }
            node.WriteUnlock();
            node = child;
        }
    }

    bool TryInsertNonFull(Node node, in TKey key, in TValue value)
    {
        while (true)
        {
            var found = node.TryGetPosition(Comparer, in key, out var position);
            if (node is LeafNode leaf)
            {
                if (found)
                {
                    node.WriteUnlock();
                    return false;
                }

                leaf.Insert(position, in key, in value);
                node.WriteUnlock();
                Interlocked.Increment(ref _length);
                return true;
            }

            var child = node.Children[position];
            child.WriteLock();
            if (child.IsFull)
            {
                SplitChild(node, position, child);
                if (Comparer.Compare(in key, in node.Keys[position]) > 0)
                {
                    child.WriteUnlock();
                    child = node.Children[position + 1];
                    if (child.IsFull)
                        throw new Exception("child was full");
                    child.WriteLock();
                }
            }
            node.WriteUnlock();
            node = child;
        }
    }

    AddOrUpdateResult TryAddOrUpdateNonFull(
        Node node, in TKey key, AddDelegate adder, UpdateDelegate updater)
    {
        while (true)
        {
            var found = node.TryGetPosition(Comparer, in key, out var position);
            if (node is LeafNode leaf)
            {
                if (found)
                {
                    updater(ref leaf.Values[position]);
                    node.WriteUnlock();
                    return AddOrUpdateResult.UPDATED;
                }
                TValue value = default;
                adder(ref value);
                leaf.Insert(position, in key, in value);
                node.WriteUnlock();
                Interlocked.Increment(ref _length);
                return AddOrUpdateResult.ADDED;
            }

            var child = node.Children[position];
            child.WriteLock();
            if (child.IsFull)
            {
                SplitChild(node, position, child);
                if (Comparer.Compare(in key, in node.Keys[position]) > 0)
                {
                    child.WriteUnlock();
                    child = node.Children[position + 1];
                    if (child.IsFull)
                        throw new Exception("child was full");
                    child.WriteLock();
                }
            }
            node.WriteUnlock();
            node = child;
        }
    }
}