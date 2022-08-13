namespace Tenray.ZoneTree.Collections.BTree;

/// <summary>
/// In memory B+Tree.
/// This class is thread-safe.
/// </summary>
/// <typeparam name="TKey">Key Type</typeparam>
/// <typeparam name="TValue">Value Type</typeparam>
public partial class BTree<TKey, TValue>
{
    public bool ContainsKey(in TKey key)
    {
        try
        {
            ReadLock();
            while (true)
            {
                var root = Root;
                root.ReadLock();
                if (root != Root)
                {
                    root.ReadUnlock();
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
                node.ReadUnlock();
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
            node.ReadLock();
            previousNode.ReadUnlock();
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
                root.ReadLock();
                if (root != Root)
                {
                    root.ReadUnlock();
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
                    node.ReadUnlock();
                    value = default;
                    return false;
                }
                value = leaf.Values[position];
                node.ReadUnlock();
                return true;
            }
            if (found)
            {
                // if key position is found with exact match
                // continue with right child.
                ++position;
            }
            var child = node.Children[position];
            child.ReadLock();
            node.ReadUnlock();
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
                root.ReadLock();
                if (root != Root)
                {
                    root.ReadUnlock();
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
                node.ReadUnlock();
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
            node.ReadLock();
            previousNode.ReadUnlock();
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