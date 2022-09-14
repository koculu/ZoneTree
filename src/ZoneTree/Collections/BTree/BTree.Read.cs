using Tenray.ZoneTree.Collections.BTree.Lock;

namespace Tenray.ZoneTree.Collections.BTree;

/// <summary>
/// In memory B+Tree.
/// This class is thread-safe.
/// </summary>
/// <typeparam name="TKey">Key Type</typeparam>
/// <typeparam name="TValue">Value Type</typeparam>
public sealed partial class BTree<TKey, TValue>
{
    public bool ContainsKey(in TKey key)
    {
        var topLevelLocker = TopLevelLocker;
        try
        {
            topLevelLocker.ReadLock();
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
            topLevelLocker.ReadUnlock();
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
        var topLevelLocker = TopLevelLocker;
        try
        {
            topLevelLocker.ReadLock();
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
            topLevelLocker.ReadUnlock();
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
        var topLevelLocker = TopLevelLocker;
        try
        {
            topLevelLocker.ReadLock();
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
            topLevelLocker.ReadUnlock();
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

    /// <summary>
    /// Converts BTree to lock-free BTree by removing top level locks and
    /// all locks from nodes. It is caller's responsibility
    /// to not to modify the tree after this method is called.
    /// It is also caller's responsibility to ensure all ongoing writes
    /// are already finished.
    /// </summary>
    public void SetTreeReadOnlyAndLockFree()
    {
        TopLevelLocker = NoLock.Instance;
        var newRoot = Root.CloneWithNoLock();

        // iterate all leafs and link them.
        var queue = new Queue<Node>();
        var leafs = new Queue<LeafNode>();
        if (newRoot is LeafNode leafRoot)
            leafs.Enqueue(leafRoot);
        else
            queue.Enqueue(newRoot);
        while (queue.Any())
        {
            var node = queue.Dequeue();
            var children = node.Children;
            if (children != null)
            {
                var len = node.Length + 1;
                for (int i = 0; i < len; i++)
                {
                    var child = children[i];
                    if (child is LeafNode leafNode)
                        leafs.Enqueue(leafNode);
                    else
                        queue.Enqueue(child);
                }
            }
        }
        var leaf = leafs.Dequeue();
        var first = leaf;
        while (leafs.Any())
        {
            var next = leafs.Dequeue();
            leaf.Next = next;
            next.Previous = leaf;
            leaf = next;
        }
        var last = leaf;
        FirstLeafNode = first;
        LastLeafNode = last;
        Root = newRoot;
        IsReadOnly = true;
    }

    public void Validate()
    {
        Root.Validate(Comparer);
    }

    public void ValidateLeafs()
    {
        var root = Root;

        var queue = new Queue<Node>();
        var leafs = new Queue<LeafNode>();
        if (root is LeafNode leafRoot)
            leafs.Enqueue(leafRoot);
        else
            queue.Enqueue(root);
        while (queue.Any())
        {
            var node = queue.Dequeue();
            var children = node.Children;
            if (children != null)
            {
                var len = node.Length + 1;
                for (int i = 0; i < len; i++)
                {
                    var child = children[i];
                    if (child is LeafNode leafNode)
                        leafs.Enqueue(leafNode);
                    else
                        queue.Enqueue(child);
                }
            }
        }
        var leaf = leafs.Dequeue();
        while (leafs.Any())
        {
            var next = leafs.Dequeue();
            if (leaf == next)
                throw new Exception("Found equal leafs on both side.");
            leaf.Next = next;
            next.Previous = leaf;
            leaf = next;
        }
    }
}