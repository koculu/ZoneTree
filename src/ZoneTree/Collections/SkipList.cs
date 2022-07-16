using System.Runtime.CompilerServices;
using Tenray.Collections;

namespace ZoneTree.Collections;

/// <summary>
/// Thread-safe SkipList implementation.
/// </summary>
/// <typeparam name="TKey">Key Type</typeparam>
/// <typeparam name="TValue">Value Type</typeparam>
public class SkipList<TKey, TValue>
{
    readonly SkipListNode Head;

    SkipListNode Tail;

    readonly Random Random = new ();

    readonly int MaxLevel;

    public IRefComparer<TKey> Comparer { get; }

    public int Length { get; private set; }

    public SkipListNode FirstNode => Head.GetNext(0);

    public SkipListNode LastNode => Tail;

    /// <summary>
    /// Choose max level according to expected skip list size.
    /// Pow(2,maxLevel) => represents optimal skip list count.
    /// Default value of maxLevel is 20, which is optimal for 1M elements.
    /// </summary>
    /// <param name="comparer">Key Comparer</param>
    /// <param name="maxLevel">maxLevel of a skip list node.</param>
    public SkipList(IRefComparer<TKey> comparer, int maxLevel = 20)
    {
        MaxLevel = maxLevel;
        Comparer = comparer;
        Head = new SkipListNode(default, maxLevel - 1);
        Tail = null;
        Head.MarkInserted();
    }

    int GetRandomLevel()
    {
        int level = 0;
        while (Random.Next(0, 2) == 1 && level < MaxLevel)
        {
            level++;
        }
        return level;
    }

    public bool TryInsert(in TKey key, in TValue value)
    {
        int level = GetRandomLevel();
        lock (Head)
        {
            var newNode = new SkipListNode(key, value, level);
            try
            {
                var node = Head;
                for (int i = MaxLevel - 1; i >= 0; --i)
                {
                    while (true)
                    {
                        var nextNode = node.GetNext(i);
                        if (nextNode == null)
                            break;

                        var r = Comparer.Compare(key, nextNode.Key);
                        if (r < 0)
                            break;

                        node = nextNode;
                    }

                    if (i <= level)
                    {
                        node.AssignNext(newNode, i, Head);
                    }
                }
                if (!newNode.HasNext)
                    Tail = newNode;
            }
            finally
            {
                newNode.MarkInserted();
            }

            ++Length;
            return true;
        }
    }

    private SkipListNode SearchNodeWithoutSpinWait(in TKey key)
    {
        var node = Head;
        var comparer = Comparer;
        for (int i = MaxLevel - 1; i >= 0; i--)
        {
            while(true)
            {
                var nextNode = node.GetNext(i);
                if (nextNode == null)
                    break;
                var r = comparer.Compare(key, nextNode.Key);
                if (r == 0)
                {
                    return nextNode;
                }
                if (r < 0)
                    break;
                node = nextNode;
            }
        }
        return null;
    }

    public SkipListNode GetLastNodeSmallerOrEqual(in TKey key)
    {
        var node = Head;
        var comparer = Comparer;
        for (int i = MaxLevel - 1; i >= 0; i--)
        {
            while(true)
            {
                node.EnsureNodeIsInserted();
                var nextNode = node.GetNext(i);
                if (nextNode == null)
                    break;

                var r = comparer.Compare(key, nextNode.Key);
                if (r == 0)
                {
                    return nextNode;
                }

                if (i == 0)
                {
                    if (r < 0)
                        return Head == node ? null : node;
                }
                else if (r < 0)
                    break;

                node = nextNode;
            }
        }
        return Head == node ? null : node;
    }

    public SkipListNode GetFirstNodeGreaterOrEqual(in TKey key)
    {
        var node = Head;
        var comparer = Comparer;
        for (int i = MaxLevel - 1; i >= 0; i--)
        {

            while (true)
            {
                node.EnsureNodeIsInserted();
                var nextNode = node.GetNext(i);
                if (nextNode == null)
                    break;
                
                var r = comparer.Compare(key, nextNode.Key);
                if (r == 0)
                {
                    return nextNode;
                }

                if (i == 0)
                {
                    if (r < 0)
                    {
                        node = node.GetNext();
                        return node;
                    }
                }
                else if (r < 0)
                    break;

                node = nextNode;
            }
        }
        if (Head == node)
        {
            node = node.GetNext();
            return node;
        }
        return null;
    }

    public delegate AddOrUpdateResult AddDelegate(SkipListNode node);

    public delegate AddOrUpdateResult UpdateDelegate(SkipListNode node);

    public AddOrUpdateResult AddOrUpdate(TKey key, AddDelegate adder, UpdateDelegate updater)
    {
        int level = GetRandomLevel();
        lock (Head)
        {
            var existingNode = SearchNodeWithoutSpinWait(in key);
            if (existingNode != null)
            {
                return updater(existingNode);
            }
            var newNode = new SkipListNode(key, level);
            var adderResult = adder(newNode);
            if (adderResult != AddOrUpdateResult.ADDED)
                return adderResult;
            try
            {
                var node = Head;
                var comparer = Comparer;
                for (int i = MaxLevel - 1; i >= 0; --i)
                {
                    while (true)
                    {
                        var nextNode = node.GetNext(i);
                        if (nextNode == null)
                            break;

                        var r = comparer.Compare(key, nextNode.Key);
                        if (r < 0)
                            break;

                        node = nextNode;
                    }

                    if (i <= level)
                    {
                        node.AssignNext(newNode, i, Head);
                    }
                }
                ++Length;
                if (!newNode.HasNext)
                    Tail = newNode;
            }
            finally
            {
                newNode.MarkInserted();
            }
            return AddOrUpdateResult.ADDED;
        }
    }

    public bool ContainsKey(in TKey key)
    {
        var node = Head;
        var comparer = Comparer;
        for (int i = MaxLevel - 1; i >= 0; i--)
        {
            while(true)
            {
                node.EnsureNodeIsInserted();
                var nextNode = node.GetNext(i);
                if (nextNode == null)
                    break;
                var r = comparer.Compare(key, nextNode.Key);
                if (r == 0)
                    return true;
                if (r < 0)
                    break;

                node = nextNode;
            }
        }
        return false;
    }

    public (TKey[] keys, TValue[] values) ToArray()
    {
        var cnt = Length;
        TKey[] keys = new TKey[cnt];
        TValue[] values = new TValue[cnt];
        var i = 0;
        var node = Head.GetNext(0);
        while (node != null && i < cnt)
        {
            keys[i] = node.Key;
            values[i] = node.Value;
            node.EnsureNodeIsInserted();
            node = node.GetNext(i);
            ++i;
        }
        return (keys, values);
    }

    public bool TryGetValue(in TKey key, out TValue value)
    {
        var node = Head;
        var comparer = Comparer;
        for (int i = MaxLevel - 1; i >= 0; i--)
        {
            while (true)
            {
                node.EnsureNodeIsInserted();
                var nextNode = node.GetNext(i);
                if (nextNode == null)
                    break;

                var r = comparer.Compare(key, nextNode.Key);
                if (r == 0)
                {
                    value = nextNode.Value;
                    return true;
                }
                if (r < 0)
                    break;

                node = nextNode;
            }
        }
        value = default;
        return false;
    }

    public bool TryRemove(in TKey key)
    {
        var node = Head;
        bool isRemoved = false;
        lock (Head)
        {
            var comparer = Comparer;
            for (int i = MaxLevel - 1; i >= 0; i--)
            {
                while (true)
                {
                    var nextNode = node.GetNext(i);
                    if (nextNode == null)
                        break;

                    var r = comparer.Compare(key, nextNode.Key);
                    if (r > 0) break;
                    if (r == 0)
                    {
                        isRemoved = true;
                        node.SetNext(i, nextNode.GetNext(i));
                        if (i == 0)
                            node.GetNext(0).SetPrevious(node);
                        break;
                    }

                    node = nextNode;
                }
            }
            if (isRemoved)
                --Length;
        }
        return isRemoved;
    }

    public class SkipListNode
    {
        /// <summary>
        /// A conforming CLI shall guarantee that read and write access 
        /// to properly aligned memory locations no larger than the native word size
        /// (the size of type native int) is atomic.
        /// </summary>
        private static readonly bool IsValueAssignmentAtomic =
            Unsafe.SizeOf<TValue>() <= IntPtr.Size;

        readonly SkipListNode[] Next;

        public readonly TKey Key;

        private TValue _value;

        public TValue Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (IsValueAssignmentAtomic)
                    return _value;
                else
                {
                    lock(this)
                    {
                        return _value;
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if(IsValueAssignmentAtomic)
                    _value = value;
                else
                {
                    lock(this)
                    {
                        _value = value;
                    }
                }
            }
        }

        public readonly int Level;

        public bool HasNext => GetNext() != null;

        public bool HasPrev => PreviousNode != null;

        volatile SkipListNode PreviousNode;

        volatile bool IsInserted;

        internal SkipListNode(in TKey key, int level)
        {
            Key = key;
            Level = level;
            Next = new SkipListNode[Level+1];
        }

        internal SkipListNode(TKey key, TValue value, int level)
        {
            Key = key;
            Value = value;
            Level = level;
            Next = new SkipListNode[Level + 1];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AssignNext(SkipListNode newNode, int i, SkipListNode head)
        {
            var nextNode = GetNext(i);
            newNode.SetNext(i, nextNode);
            SetNext(i, newNode);
            if (i == 0)
            {
                if (nextNode != null)
                    nextNode.PreviousNode = head == newNode ? null : newNode;
                newNode.PreviousNode = head == this ? null : this;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SkipListNode GetNext()
        {
            return Volatile.Read(ref Next[0]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SkipListNode GetNext(int level)
        {
            return Volatile.Read(ref Next[level]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SetNext(int level, SkipListNode node)
        {
            Volatile.Write(ref Next[level], node);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SkipListNode GetPrevious()
        {
            return PreviousNode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SetPrevious(SkipListNode node)
        {
            PreviousNode = node;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void MarkInserted()
        {
            IsInserted = true;
        }

        /// <summary>
        /// Spin waits till the node is fully inserted.
        /// Call this method before you access the Next or Prev
        /// pointers of a node.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureNodeIsInserted()
        {
            if (IsInserted)
                return;
            var spinWait = new SpinWait();
            while (!IsInserted)
            {
                spinWait.SpinOnce();
            }
        }
    }
}
