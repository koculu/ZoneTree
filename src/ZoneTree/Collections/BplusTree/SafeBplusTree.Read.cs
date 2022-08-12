namespace Tenray.ZoneTree.Collections.BplusTree;

/// <summary>
/// In memory B+Tree.
/// This class is thread-safe.
/// </summary>
/// <typeparam name="TKey">Key Type</typeparam>
/// <typeparam name="TValue">Value Type</typeparam>
public partial class SafeBplusTree<TKey, TValue>
{
    public bool ContainsKey(in TKey key)
    {
        try
        {
            ReadLock();
            while (true)
            {
                var root = Root;
                root.LockForRead();
                if (root != Root)
                {
                    root.UnlockForRead();
                    continue;
                }
                return ContainsKey(root, in key);
            }
        }
        finally
        {
            ReadUnlock();
        }
    }

    bool ContainsKey(Node node, in TKey key)
    {
        while (node != null)
        {
            var found = node.TryGetPosition(Comparer, in key, out var position);
            if (node is LeafNode)
            {
                node.UnlockForRead();
                return found;
            }
            if (found)
            {
                // if key position is found with exact match
                // continue with right child.
                ++position;
            }
            var previousNode = node;
            node = node.Children[position];
            node.LockForRead();
            previousNode.UnlockForRead();
        }
        return false;
    }

    public bool TryGetValue(in TKey key, out TValue value)
    {
        try
        {
            ReadLock();
            while (true)
            {
                var root = Root;
                root.LockForRead();
                if (root != Root)
                {
                    root.UnlockForRead();
                    continue;
                }
                return TryGetValue(root, in key, out value);
            }
        }
        finally
        {
            ReadUnlock();
        }
    }

    bool TryGetValue(Node node, in TKey key, out TValue value)
    {
        while (node != null)
        {
            var found = node.TryGetPosition(Comparer, in key, out var position);
            if (node is LeafNode leaf)
            {
                if (!found)
                {
                    node.UnlockForRead();
                    value = default;
                    return false;
                }
                value = leaf.Values[position];
                node.UnlockForRead();
                return true;
            }
            if (found)
            {
                // if key position is found with exact match
                // continue with right child.
                ++position;
            }
            var child = node.Children[position];
            child.LockForRead();
            node.UnlockForRead();
            node = child;
        }
        value = default;
        return false;
    }

    LeafNode GetLeafNode(in TKey key)
    {
        try
        {
            ReadLock();
            while (true)
            {
                var root = Root;
                root.LockForRead();
                if (root != Root)
                {
                    root.UnlockForRead();
                    continue;
                }
                return GetLeafNode(root, in key);
            }
        }
        finally
        {
            ReadUnlock();
        }
    }

    LeafNode GetLeafNode(Node node, in TKey key)
    {
        while (true)
        {
            var found = node.TryGetPosition(Comparer, in key, out var position);
            if (node is LeafNode leaf)
            {
                node.UnlockForRead();
                return leaf;
            }
            if (found)
            {
                // if key position is found with exact match
                // continue with right child.
                ++position;
            }
            var previousNode = node;
            node = node.Children[position];
            if (node == null)
                node = node.Children[position - 1];
            node.LockForRead();
            previousNode.UnlockForRead();
        }
    }

    LeafNode GetFrozenLeafNode(in TKey key)
    {
        var root = Root;
        return GetFrozenLeafNode(root, in key);
    }

    LeafNode GetFrozenLeafNode(Node node, in TKey key)
    {
        while (true)
        {
            var found = node.TryGetPosition(Comparer, in key, out var position);
            if (node is LeafNode leaf)
            {
                return leaf;
            }
            if (found)
            {
                // if key position is found with exact match
                // continue with right child.
                ++position;
            }
            node = node.Children[position];
            if (node == null)
                node = node.Children[position - 1];
        }
    }


}