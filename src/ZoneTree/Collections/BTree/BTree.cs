﻿using Tenray.ZoneTree.Collections.BTree.Lock;
using Tenray.ZoneTree.Comparers;
using Tenray.ZoneTree.Core;

namespace Tenray.ZoneTree.Collections.BTree;

/// <summary>
/// In memory B+Tree.
/// This class is thread-safe.
/// </summary>
/// <typeparam name="TKey">Key Type</typeparam>
/// <typeparam name="TValue">Value Type</typeparam>
public sealed partial class BTree<TKey, TValue>
{
    ILocker TopLevelLocker;

    readonly int NodeSize = 128;

    readonly int LeafSize = 128;

    volatile Node Root;

    volatile LeafNode FirstLeafNode;

    volatile LeafNode LastLeafNode;

    public readonly IRefComparer<TKey> Comparer;

    volatile int _length;

    public int Length => _length;

    public readonly BTreeLockMode LockMode;

    public IIncrementalIdProvider OpIndexProvider { get; }

    volatile bool _isReadOnly;

    public bool IsReadOnly { get => _isReadOnly; set => _isReadOnly = value; }

    public BTree(
        IRefComparer<TKey> comparer,
        BTreeLockMode lockMode,
        IIncrementalIdProvider indexOpProvider = null,
        int nodeSize = 128,
        int leafSize = 128)
    {
        NodeSize = nodeSize;
        LeafSize = leafSize;
        Comparer = comparer;
        OpIndexProvider = indexOpProvider ?? new IncrementalIdProvider();
        TopLevelLocker = lockMode switch
        {
            BTreeLockMode.TopLevelMonitor => new MonitorLock(),
            BTreeLockMode.TopLevelReaderWriter => new ReadWriteLock(),
            BTreeLockMode.NoLock or BTreeLockMode.NodeLevelMonitor or BTreeLockMode.NodeLevelReaderWriter => NoLock.Instance,
            _ => throw new NotSupportedException(),
        };
        LockMode = lockMode;
        Root = new LeafNode(GetNodeLocker(), leafSize);
        FirstLeafNode = Root as LeafNode;
        LastLeafNode = FirstLeafNode;
    }

    ILocker GetNodeLocker() => LockMode switch
    {
        BTreeLockMode.TopLevelMonitor or
        BTreeLockMode.TopLevelReaderWriter or
        BTreeLockMode.NoLock => NoLock.Instance,

        BTreeLockMode.NodeLevelMonitor => new MonitorLock(),
        BTreeLockMode.NodeLevelReaderWriter => new ReadWriteLock(),
        _ => throw new NotSupportedException(),
    };

    public void WriteLock()
    {
        TopLevelLocker.WriteLock();
    }

    public void WriteUnlock()
    {
        TopLevelLocker.WriteUnlock();
    }

    public NodeIterator GetIteratorWithLastKeySmallerOrEqual(in TKey key)
    {
        var topLevelLocker = TopLevelLocker;
        try
        {
            topLevelLocker.ReadLock();
            var iterator = GetLeafNode(key).GetIterator(this);
            return iterator.SeekLastKeySmallerOrEqual(Comparer, in key);
        }
        finally
        {
            topLevelLocker.ReadUnlock();
        }
    }

    public NodeIterator GetIteratorWithFirstKeyGreaterOrEqual(in TKey key)
    {
        var topLevelLocker = TopLevelLocker;
        try
        {
            topLevelLocker.ReadLock();
            var iterator = GetLeafNode(key).GetIterator(this);
            return iterator.SeekFirstKeyGreaterOrEqual(Comparer, in key);
        }
        finally
        {
            topLevelLocker.ReadUnlock();
        }
    }

    public FrozenNodeIterator GetFrozenIteratorWithLastKeySmallerOrEqual(in TKey key)
    {
        var iterator = GetFrozenLeafNode(key).CreateFrozenIterator();
        return iterator.SeekLastKeySmallerOrEqual(Comparer, in key);
    }

    public FrozenNodeIterator GetFrozenIteratorWithFirstKeyGreaterOrEqual(in TKey key)
    {
        var iterator = GetFrozenLeafNode(key).CreateFrozenIterator();
        return iterator.SeekFirstKeyGreaterOrEqual(Comparer, in key);
    }

    public NodeIterator GetFirstIterator()
    {
        var topLevelLocker = TopLevelLocker;
        try
        {
            topLevelLocker.ReadLock();
            return FirstLeafNode.GetIterator(this);
        }
        finally
        {
            topLevelLocker.ReadUnlock();
        }
    }

    public FrozenNodeIterator GetFrozenFirstIterator()
    {
        return FirstLeafNode.CreateFrozenIterator();
    }

    public NodeIterator GetLastIterator()
    {
        var topLevelLocker = TopLevelLocker;
        try
        {
            topLevelLocker.ReadLock();
            return LastLeafNode.GetIterator(this);
        }
        finally
        {
            topLevelLocker.ReadUnlock();
        }
    }

    public FrozenNodeIterator GetFrozenLastIterator()
    {
        return LastLeafNode.CreateFrozenIterator();
    }

    public void SetNextOpIndex(long nextId)
        => OpIndexProvider.SetNextId(nextId);

    public long LastOpIndex => OpIndexProvider.LastId;
}