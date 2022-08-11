namespace Tenray.ZoneTree.Collections;

/// <summary>
/// In memory B+Tree.
/// This class is thread-safe.
/// </summary>
/// <typeparam name="TKey">Key Type</typeparam>
/// <typeparam name="TValue">Value Type</typeparam>
public class SafeBplusTree<TKey, TValue>
{
    readonly int NodeSize = 128;

    readonly int LeafSize = 128;

    volatile Node Root;

    readonly LeafNode FirstLeafNode;
    
    volatile LeafNode LastLeafNode;

    public readonly IRefComparer<TKey> Comparer;

    readonly object WriteLock = new();

    volatile int _length;

    public int Length => _length;

    /*public LeafNode First => FirstLeafNode;
    
    public LeafNode Last => LastLeafNode;*/

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

    public bool Upsert(in TKey key, in TValue value)
    {
        lock (WriteLock)
        {
            var root = Root;
            if (!root.IsFull)
            {
                return UpsertNonFull(root, in key, in value);
            }
            var newRoot = new Node(NodeSize);
            newRoot.Children[0] = root;
            SplitChild(newRoot, 0, root);
            var result = UpsertNonFull(newRoot, in key, in value);
            Root = newRoot;
            return result;
        }
    }

    public bool TryInsert(in TKey key, in TValue value)
    {
        lock (WriteLock)
        {
            var root = Root;
            if (!root.IsFull)
            {
                return TryInsertNonFull(root, in key, in value);
            }
            var newRoot = new Node(NodeSize);
            newRoot.Children[0] = root;
            SplitChild(newRoot, 0, root);
            var result = TryInsertNonFull(newRoot, in key, in value);
            Root = newRoot;
            return result;
        }
    }

    public delegate AddOrUpdateResult AddDelegate(ref TValue value);

    public delegate AddOrUpdateResult UpdateDelegate(ref TValue value);

    public AddOrUpdateResult AddOrUpdate(
        in TKey key,
        AddDelegate adder, 
        UpdateDelegate updater)
    {
        lock (WriteLock)
        {
            var root = Root;
            if (!root.IsFull)
            {
                return TryAddOrUpdateNonFull(root, in key, adder, updater);
            }
            var newRoot = new Node(NodeSize);
            newRoot.Children[0] = root;
            SplitChild(newRoot, 0, root);
            AddOrUpdateResult result =
                TryAddOrUpdateNonFull(newRoot, in key, adder, updater);
            Root = newRoot;
            return result;
        }
    }

    public NodeIterator GetIteratorWithLastKeySmallerOrEqual(in TKey key)
    {
        var iterator = GetLeafNode(key).GetIterator();
        return iterator.SeekLastKeySmallerOrEqual(Comparer, in key);
    }

    public NodeIterator GetIteratorWithFirstKeyGreaterOrEqual(in TKey key)
    {
        var iterator = GetLeafNode(key).GetIterator();
        return iterator.SeekFirstKeyGreaterOrEqual(Comparer, in key);
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

    LeafNode GetLeafNode(in TKey key)
    {
        while (true)
        {
            var root = Root;
            var result = GetLeafNode(root, in key);
            if (root == Root)
                return result;
            // if Root is splitted in the middle of the ContainsKey query,
            // restart search.
        }
    }

    LeafNode GetLeafNode(Node node, in TKey key)
    {
        while (true)
        {
            // Temp node to avoid changing node inside node lock.
            Node tmp;
            lock (node)
            {
                // remove split lock because we already locked the node.
                node.RemoveSplitLock();

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
                tmp = node.Children[position];
                if (tmp == null)
                    tmp = node.Children[position - 1];
                // tmp cannot split now because of parent lock.
                tmp.AddSplitLock();
            }
            // tmp parent's lock released. but tmp still cannot be splitted.
            // because of AddSplitLock above.
            node = tmp;
        }
    }

    LeafNode GetFrozenLeafNode(in TKey key)
    {
        while (true)
        {
            var root = Root;
            return GetFrozenLeafNode(root, in key);
        }
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

    public NodeIterator GetFirstIterator()
    {
        return FirstLeafNode.GetIterator();
    }

    public FrozenNodeIterator GetFrozenFirstIterator()
    {
        return FirstLeafNode.GetFrozenIterator();
    }

    public NodeIterator GetLastIterator()
    {
        return LastLeafNode.GetIterator();
    }
    
    public FrozenNodeIterator GetFrozenLastIterator()
    {
        return LastLeafNode.GetFrozenIterator();
    }

    public bool ContainsKey(in TKey key)
    {
        var root = Root;
        var result = ContainsKey(root, in key);
        // if Root is splitted in the middle of the ContainsKey query,
        // and key is not found, restart search.
        if (!result && root != Root)
            return ContainsKey(in key);
        return result;
    }

    bool ContainsKey(Node node, in TKey key)
    {
        while (node != null)
        {
            // Temp node to avoid changing node inside node lock.
            Node tmp;
            lock (node)
            {
                // remove split lock because we already locked the node.
                node.RemoveSplitLock();

                var found = node.TryGetPosition(Comparer, in key, out var position);
                if (node is LeafNode)
                {
                    return found;
                }
                if (found)
                {
                    // if key position is found with exact match
                    // continue with right child.
                    ++position;
                }
                tmp = node.Children[position];
                // tmp cannot split now because of parent lock.
                tmp.AddSplitLock();
            }
            // tmp parent's lock released. but tmp still cannot be splitted.
            // because of AddSplitLock above.
            node = tmp;
        }
        return false;
    }

    public bool TryGetValue(in TKey key, out TValue value)
    {
        while (true)
        {
            var root = Root;
            var result = TryGetValue(root, in key, out value);
            if (result || root == Root)
                return result;
            // if Root is splitted in the middle of the ContainsKey query,
            // and key is not found, restart search.
        }
    }

    bool TryGetValue(Node node, in TKey key, out TValue value)
    {
        var addedSplitLock = false;
        while(node != null)
        {
            // Temp node to avoid changing node inside node lock.
            Node tmp;
            lock (node)
            {
                // remove split lock because we already locked the node.
                if (addedSplitLock)
                    node.RemoveSplitLock();

                var found = node.TryGetPosition(Comparer, in key, out var position);
                if (node is LeafNode leaf)
                {
                    if (!found)
                    {
                        value = default;
                        return false;
                    }
                    value = leaf.Values[position];
                    return true;
                }
                if (found)
                {
                    // if key position is found with exact match
                    // continue with right child.
                    ++position;
                }
                tmp = node.Children[position];
                // tmp cannot split now because of parent lock.
                tmp.AddSplitLock();
                addedSplitLock = true;
            }
            // tmp parent's lock released. but tmp still cannot be splitted.
            // because of AddSplitLock above.
            node = tmp;
        }
        value = default;
        return false;
    }

    public (TKey[] keys, TValue[] values) ToArray()
    {
        var len = Length;
        var keys = new TKey[len];
        var values = new TValue[len];
        var node = FirstLeafNode;
        var off = 0;
        while (node != null)
        {
            var tmp = node;
            lock (tmp)
            {
                var nodeKeys = node.Keys;
                var nodeValues = node.Values;
                for (var i = 0; i < node.Length; ++i)
                {
                    keys[off] = nodeKeys[i];
                    values[off] = nodeValues[i];
                    ++off;
                }
                node = node.Next;
            }
        }
        return (keys, values);
    }

    public TKey[] GetKeys()
    {
        var len = Length;
        var keys = new TKey[len];
        var node = FirstLeafNode;
        var off = 0;
        while (node != null)
        {
            var tmp = node;
            lock (tmp)
            {
                var nodeKeys = node.Keys;
                for (var i = 0; i < node.Length; ++i)
                {
                    keys[off] = nodeKeys[i];
                    ++off;
                }
                node = node.Next;
            }
        }
        return keys;
    }

    public TValue[] GetValues()
    {
        var len = Length;
        var values = new TValue[len];
        var node = FirstLeafNode;
        var off = 0;
        while (node != null)
        {
            var tmp = node;
            lock (tmp)
            {
                var nodeValues = node.Values;
                for (var i = 0; i < node.Length; ++i)
                {
                    values[off] = nodeValues[i];
                    ++off;
                }
                node = node.Next;
            }
        }
        return values;
    }

    void SplitChild(Node parent, int rightChildPosition, Node leftNode)
    {
        var spinWait = new SpinWait();
        while(!TrySplitChild(parent, rightChildPosition, leftNode))
        {
            spinWait.SpinOnce();
        }
    }

    bool TrySplitChild(Node parent, int rightChildPosition, Node leftNode)
    {
        lock (parent)
        {
            var pivotPosition = (leftNode.Length + 1) / 2;
            // tree is locked, safe to read from any node.
            ref var pivotKey = ref leftNode.Keys[pivotPosition];  
            if (leftNode is LeafNode leftLeaf)
            {
                var rightLeaf = new LeafNode(LeafSize);
                // updating leftNode, lock for readers!
                lock (leftNode)
                {
                    // A reader adds split lock. return and retry.
                    // Expected to be very low probability in theory. Need tests.
                    if (leftNode.IsSplitLocked())
                        return false;

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
                        // updating left node -> next, lock for readers!
                        lock (leftNext)
                        {
                            parent.InsertKeyAndChild(rightChildPosition, in pivotKey, rightLeaf);
                            rightLeaf.ReplaceFrom(leftLeaf, pivotPosition);
                            if (LastLeafNode == leftLeaf)
                                LastLeafNode = rightLeaf;
                        }
                    }
                }
            }
            else
            {
                // updating leftNode, lock for readers!
                lock (leftNode)
                {
                    // A reader adds split lock. return and retry.
                    // Expected to be very low probability in theory. Need tests.
                    if (leftNode.IsSplitLocked())
                        return false;

                    var rightNode = new Node(NodeSize);
                    parent.InsertKeyAndChild(rightChildPosition, in pivotKey, rightNode);
                    rightNode.ReplaceFrom(leftNode, pivotPosition);
                }
            }
        }
        return true;
    }

    bool UpsertNonFull(Node node, in TKey key, in TValue value)
    {
        while (true)
        {
            // tree is locked, safe to read from any node.
            var found = node.TryGetPosition(Comparer, in key, out var position);
            if (node is LeafNode leaf)
            {
                // leaf locks itself for readers!
                if (found)
                {
                    leaf.Update(position, in key, in value);
                    return false;
                }
                leaf.Insert(position, in key, in value);
                ++_length;
                return true;
            }

            var child = node.Children[position];
            if (child.IsFull)
            {
                SplitChild(node, position, child);
                if (Comparer.Compare(in key, in node.Keys[position]) > 0)
                    ++position;
            }
            node = node.Children[position];
        }
    }

    bool TryInsertNonFull(Node node, in TKey key, in TValue value)
    {
        while (true)
        {
            // tree is locked, safe to read from any node.
            var found = node.TryGetPosition(Comparer, in key, out var position);
            if (node is LeafNode leaf)
            {
                if (found)
                    return false;
                // leaf locks itself for readers!
                leaf.Insert(position, in key, in value); 
                ++_length;
                return true;
            }

            var child = node.Children[position];
            if (child.IsFull)
            {
                SplitChild(node, position, child);
                if (Comparer.Compare(in key, in node.Keys[position]) > 0)
                    ++position;
            }
            node = node.Children[position];
        }
    }

    AddOrUpdateResult TryAddOrUpdateNonFull(
        Node node, in TKey key, AddDelegate adder, UpdateDelegate updater)
    {
        while (true)
        {
            // tree is locked, safe to read from any node.
            var found = node.TryGetPosition(Comparer, in key, out var position);
            if (node is LeafNode leaf)
            {
                if (found)
                {
                    // updating leaf, lock for readers!
                    lock (leaf)
                    {
                        updater(ref leaf.Values[position]);
                    }
                    return AddOrUpdateResult.UPDATED;
                }
                TValue value = default;
                adder(ref value);

                // leaf locks itself for readers!
                leaf.Insert(position, in key, in value);

                ++_length;
                return AddOrUpdateResult.ADDED;
            }

            var child = node.Children[position];
            if (child.IsFull)
            {
                SplitChild(node, position, child);
                if (Comparer.Compare(in key, in node.Keys[position]) > 0)
                    ++position;
            }
            node = node.Children[position];
        }
    }

    public class Node
    {
        public TKey[] Keys;

        public Node[] Children;

        public int Length = 0;

        public bool IsFull => Keys.Length == Length;

        public Node()
        {
        }

        public Node(int nodeSize)
        {
            Keys = new TKey[nodeSize];
            Children = new Node[nodeSize + 1];
        }

        public bool TryGetPosition(
            IRefComparer<TKey> comparer,
            in TKey key, 
            out int position)
        {
            var list = Keys;
            int l = 0, r = Length - 1;
            while (l <= r)
            {
                int m = (l + r) / 2;
                var rec = list[m];
                var res = comparer.Compare(in rec, in key);
                if (res == 0)
                {
                    position = m;
                    return true;
                }
                if (res < 0)
                    l = m + 1;
                else
                    r = m - 1;
            }
            position = r + 1;
            return false;
        }

        public void InsertKeyAndChild(int position, in TKey key, Node child)
        {
            lock (this)
            {
                var len = Length - position;
                if (len > 0)
                {
                    Array.Copy(Keys, position, Keys, position + 1, len);
                    Array.Copy(Children, position + 1, Children, position + 2, len);
                }
                Keys[position] = key;
                Children[position + 1] = child;
                ++Length;
            }
        }

        public void ReplaceFrom(Node leftNode, int position)
        {
            var rightLen = leftNode.Length - position;
            leftNode.Length = position;
            Length = rightLen;
            
            int i = 0, j = position;
            for (; i < rightLen; ++i, ++j)
            {
                Children[i] = leftNode.Children[j];
                Keys[i] = leftNode.Keys[j];
            }
            Children[i] = leftNode.Children[j];
        }

        volatile int SplitLockCount;

        public void AddSplitLock()
        {
            Interlocked.Increment(ref SplitLockCount);
        }

        public void RemoveSplitLock()
        {
            Interlocked.Decrement(ref SplitLockCount);
        }

        public bool IsSplitLocked()
        {
            return SplitLockCount > 0;
        }
    }

    public class LeafNode : Node
    {
        public TValue[] Values;

        public LeafNode Previous;

        public LeafNode Next;

        public LeafNode(int leafSize)
        {
            Keys = new TKey[leafSize]; 
            Values = new TValue[leafSize];
        }

        public void Update(int position, in TKey key, in TValue value)
        {
            lock (this)
            {
                Keys[position] = key;
                Values[position] = value;
            }
        }

        public void Insert(int position, in TKey key, in TValue value)
        {
            lock (this)
            {
                var len = Length - position;
                if (len > 0)
                {
                    Array.Copy(Keys, position, Keys, position + 1, len);
                    Array.Copy(Values, position, Values, position + 1, len);
                }
                Keys[position] = key;
                Values[position] = value;
                ++Length;
            }
        }

        public void ReplaceFrom(LeafNode leftLeaf, int position)
        {
            var rightLen = leftLeaf.Length - position;
            leftLeaf.Length = position;
            Length = rightLen;

            for (int i = 0, j = position; i < rightLen; ++i, ++j) {
                Keys[i] = leftLeaf.Keys[j];
                Values[i] = leftLeaf.Values[j];
            }

            Next = leftLeaf.Next;
            if (Next != null)
                Next.Previous = this;
            leftLeaf.Next = this;
            Previous = leftLeaf;
        }

        public NodeIterator GetIterator()
        {
            lock(this)
            {
                var keys = new TKey[Length];
                var values = new TValue[Length];
                Array.Copy(Keys, 0, keys, 0, Length);
                Array.Copy(Values, 0, values, 0, Length);
                return new NodeIterator(this, keys, values);
            }
        }
        public FrozenNodeIterator GetFrozenIterator()
        {
            return new FrozenNodeIterator(this);
        }
    }

    public class NodeIterator
    {
        public LeafNode Node { get; }

        public TKey[] Keys { get; }

        public TValue[] Values { get; }

        public TKey CurrentKey => Keys[CurrentIndex];
        
        public TValue CurrentValue => Values[CurrentIndex];

        public bool HasCurrent => CurrentIndex >= 0 && CurrentIndex < Keys.Length;

        int CurrentIndex = -1;

        public NodeIterator(LeafNode leafNode, TKey[] keys, TValue[] values)
        {
            Node = leafNode;
            Keys = keys;
            Values = values;
        }

        public NodeIterator GetPreviousNodeIterator()
        {
            //lock (Node)
            {
                return Node.Previous?.GetIterator();
            }
        }

        public NodeIterator GetNextNodeIterator()
        {
            //lock (Node)
            {
                return Node.Next?.GetIterator();
            }
        }

        public bool HasNext()
        {
            return CurrentIndex + 1 < Keys.Length;
        }

        public bool Next()
        {
            if (!HasNext())
                return false;
            ++CurrentIndex;
            return CurrentIndex < Keys.Length;
        }

        public bool HasPrevious()
        {
            return CurrentIndex > 0;
        }

        public bool Previous()
        {
            if (!HasPrevious())
                return false;
            --CurrentIndex;
            return CurrentIndex >= 0;
        }
        
        public void SeekBegin()
        {
            CurrentIndex = 0;
        }

        public void SeekEnd()
        {
            CurrentIndex = Keys.Length - 1;
        }

        public NodeIterator SeekLastKeySmallerOrEqual(
            IRefComparer<TKey> comparer, in TKey key)
        {
            var iterator = this;
            while (iterator != null)
            {
                var pos =
                    iterator.GetLastSmallerOrEqualPosition(comparer, in key);
                if (pos == -1)
                {
                    iterator = iterator.GetPreviousNodeIterator();
                    continue;
                }
                iterator.CurrentIndex = pos;
                return iterator;
            }
            return null;
        }

        public NodeIterator SeekFirstKeyGreaterOrEqual(
            IRefComparer<TKey> comparer, in TKey key)
        {
            var iterator = this;
            while (iterator != null)
            {
                var pos = 
                    iterator.GetFirstGreaterOrEqualPosition(comparer, in key);
                if (pos == iterator.Keys.Length)
                {
                    iterator = iterator.GetNextNodeIterator();
                    continue;
                }
                iterator.CurrentIndex = pos;
                return iterator;
            }
            return null;
        }

        /// <summary>
        /// Finds the position of element that is smaller or equal than key.
        /// </summary>
        /// <param name="key">The key</param>
        /// <returns>-1 or a valid position</returns>
        public int GetLastSmallerOrEqualPosition(
            IRefComparer<TKey> comparer, in TKey key)
        {
            var x = GetFirstGreaterOrEqualPosition(comparer, in key);
            if (x == -1)
                return -1;
            if (x == Keys.Length)
                return x - 1;
            if (comparer.Compare(in key, Keys[x]) == 0)
                return x;
            return x - 1;
        }

        /// <summary>
        /// Finds the position of element that is greater or equal than key.
        /// </summary>
        /// <param name="key">The key</param>
        /// <returns>The length of the segment or a valid position</returns>
        public int GetFirstGreaterOrEqualPosition(
            IRefComparer<TKey> comparer, in TKey key)
        {
            // This is the lower bound algorithm.
            var list = Keys;
            int l = 0, h = list.Length;
            var comp = comparer;
            while (l < h)
            {
                int mid = l + (h - l) / 2;
                if (comp.Compare(in key, list[mid]) <= 0)
                    h = mid;
                else
                    l = mid + 1;
            }
            return l;
        }
    }

    public class FrozenNodeIterator
    {
        public LeafNode Node { get; }

        public TKey CurrentKey => Node.Keys[CurrentIndex];

        public TValue CurrentValue => Node.Values[CurrentIndex];

        public bool HasCurrent => CurrentIndex >= 0 && CurrentIndex < Node.Length;

        int CurrentIndex = -1;

        public FrozenNodeIterator(LeafNode leafNode)
        {
            Node = leafNode;
        }

        public FrozenNodeIterator GetPreviousNodeIterator()
        {
            return Node.Previous?.GetFrozenIterator();
        }

        public FrozenNodeIterator GetNextNodeIterator()
        {
            return Node.Next?.GetFrozenIterator();
        }

        public bool HasNext()
        {
            return CurrentIndex + 1 < Node.Length;
        }

        public bool Next()
        {
            if (!HasNext())
                return false;
            ++CurrentIndex;
            return CurrentIndex < Node.Length;
        }

        public bool HasPrevious()
        {
            return CurrentIndex > 0;
        }

        public bool Previous()
        {
            if (!HasPrevious())
                return false;
            --CurrentIndex;
            return CurrentIndex >= 0;
        }

        public void SeekBegin()
        {
            CurrentIndex = 0;
        }

        public void SeekEnd()
        {
            CurrentIndex = Node.Length - 1;
        }

        public FrozenNodeIterator SeekLastKeySmallerOrEqual(
            IRefComparer<TKey> comparer, in TKey key)
        {
            var iterator = this;
            while (iterator != null)
            {
                var pos =
                    iterator.GetLastSmallerOrEqualPosition(comparer, in key);
                if (pos == -1)
                {
                    iterator = iterator.GetPreviousNodeIterator();
                    continue;
                }
                iterator.CurrentIndex = pos;
                return iterator;
            }
            return null;
        }

        public FrozenNodeIterator SeekFirstKeyGreaterOrEqual(
            IRefComparer<TKey> comparer, in TKey key)
        {
            var iterator = this;
            while (iterator != null)
            {
                var pos =
                    iterator.GetFirstGreaterOrEqualPosition(comparer, in key);
                if (pos == iterator.Node.Keys.Length)
                {
                    iterator = iterator.GetNextNodeIterator();
                    continue;
                }
                iterator.CurrentIndex = pos;
                return iterator;
            }
            return null;
        }

        /// <summary>
        /// Finds the position of element that is smaller or equal than key.
        /// </summary>
        /// <param name="key">The key</param>
        /// <returns>-1 or a valid position</returns>
        public int GetLastSmallerOrEqualPosition(
            IRefComparer<TKey> comparer, in TKey key)
        {
            var x = GetFirstGreaterOrEqualPosition(comparer, in key);
            if (x == -1)
                return -1;
            if (x == Node.Length)
                return x - 1;
            if (comparer.Compare(in key, Node.Keys[x]) == 0)
                return x;
            return x - 1;
        }

        /// <summary>
        /// Finds the position of element that is greater or equal than key.
        /// </summary>
        /// <param name="key">The key</param>
        /// <returns>The length of the segment or a valid position</returns>
        public int GetFirstGreaterOrEqualPosition(
            IRefComparer<TKey> comparer, in TKey key)
        {
            // This is the lower bound algorithm.
            var list = Node.Keys;
            int l = 0, h = Node.Length;
            var comp = comparer;
            while (l < h)
            {
                int mid = l + (h - l) / 2;
                if (comp.Compare(in key, list[mid]) <= 0)
                    h = mid;
                else
                    l = mid + 1;
            }
            return l;
        }
    }
}