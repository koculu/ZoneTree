using Tenray.ZoneTree.Exceptions;

namespace Tenray.ZoneTree.Collections.BTree;

public delegate TValue GetValueDelegate<TKey, TValue>(long opIndex);

/// <summary>
/// In memory B+Tree.
/// This class is thread-safe.
/// </summary>
/// <typeparam name="TKey">Key Type</typeparam>
/// <typeparam name="TValue">Value Type</typeparam>
public sealed partial class BTree<TKey, TValue>
{
    public bool Upsert(in TKey key, GetValueDelegate<TKey, TValue> valueGetter, out TValue value, out long opIndex)
    {
        if (IsReadOnly)
            throw new BTreeIsReadOnlyException();
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
                    return UpsertNonFull(root, in key, valueGetter, out value, out opIndex);
                }
                var newRoot = new Node(GetNodeLocker(), NodeSize);
                newRoot.Children[0] = root;
                newRoot.WriteLock();
                TrySplitChild(newRoot, 0, root);
                var result = UpsertNonFull(newRoot, in key, valueGetter, out value, out opIndex);
                Root = newRoot;
                root.WriteUnlock();
                return result;
            }
        }
        catch (Exception)
        {
            Root.WriteUnlock();
            throw;
        }
        finally
        {
            WriteUnlock();
        }
    }

    bool UpsertNonFull(Node node, in TKey key, GetValueDelegate<TKey, TValue> valueGetter, out TValue value, out long opIndex)
    {
        while (true)
        {
            var found = node.TryGetPosition(Comparer, in key, out var position);
            if (node is LeafNode leaf)
            {
                opIndex = OpIndexProvider.NextId();
                if (found)
                {
                    value = valueGetter(opIndex);
                    leaf.Update(position, in key, value);
                    node.WriteUnlock();
                    return false;
                }
                value = valueGetter(opIndex);
                leaf.Insert(position, in key, value);
                Interlocked.Increment(ref _length);
                node.WriteUnlock();
                return true;
            }
            if (found)
                ++position;
            var child = node.Children[position];
            child.WriteLock();
            if (child.IsFull)
            {
                var splitted = TrySplitChild(node, position, child);
                child.WriteUnlock();
                if (!splitted)
                {
                    continue;
                }

                if (Comparer.Compare(in key, in node.Keys[position]) >= 0)
                    ++position;

                child = node.Children[position];
                child.WriteLock();
            }
            node.WriteUnlock();
            node = child;
        }
    }
}