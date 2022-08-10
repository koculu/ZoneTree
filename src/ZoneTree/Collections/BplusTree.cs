namespace Tenray.ZoneTree.Collections;

public class BplusTree<TKey, TValue>
{
    int NodeSize = 128;

    int LeafSize = 128;

    volatile Node Root;

    volatile LeafNode FirstLeafNode;
    
    volatile LeafNode LastLeafNode;

    readonly IRefComparer<TKey> Comparer;

    public LeafNode First => FirstLeafNode;
    
    public LeafNode Last => LastLeafNode;

    public BplusTree(
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

    public void Insert(in TKey key, in TValue value)
    {
        var root = Root;
        if (!root.IsFull)
        {
            InsertNonFull(root, in key, in value);
            return;
        }        
        var newRoot = new Node(NodeSize);
        newRoot.Children[0] = root;
        SplitChild(newRoot, 0, root);
        InsertNonFull(newRoot, in key, in value);
        Root = newRoot;
    }

    public bool TryGetValue(in TKey key, out TValue value)
    {
        return TryGetValue(Root, in key, out value);
    }

    bool TryGetValue(Node node, in TKey key, out TValue value)
    {
        while(node != null)
        {
            var found = node.TryGetPosition(Comparer, in key, out var position);
            if (node is LeafNode leaf)
            {
                if (position == -1)
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
            node = node.Children[position];
        }
        value = default;
        return false;
    }

    void SplitChild(Node parent, int rightChildPosition, Node leftNode)
    {
        var pivotPosition = (leftNode.Length + 1) / 2;
        ref var pivotKey = ref leftNode.Keys[pivotPosition];
        if (leftNode is LeafNode leftLeaf)
        {
            var rightLeaf = new LeafNode(LeafSize);
            parent.InsertKeyAndChild(rightChildPosition, in pivotKey, rightLeaf);
            rightLeaf.ReplaceFrom(leftLeaf, pivotPosition);
            if (LastLeafNode == leftLeaf)
                LastLeafNode = rightLeaf;
        }
        else
        {
            var rightNode = new Node(NodeSize);
            parent.InsertKeyAndChild(rightChildPosition, in pivotKey, rightNode);
            rightNode.ReplaceFrom(leftNode, pivotPosition);
        }
    }

    void InsertNonFull(Node node, in TKey key, in TValue value)
    {
        while (true)
        {
            node.TryGetPosition(Comparer, in key, out var position);
            if (node is LeafNode leaf)
            {
                leaf.Insert(position, in key, in value);
                return;
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

        public void Insert(int position, in TKey key, in TValue value)
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
    }
}