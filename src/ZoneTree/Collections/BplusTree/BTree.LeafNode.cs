namespace Tenray.ZoneTree.Collections.BTree;

public partial class BTree<TKey, TValue>
{
    public class LeafNode : Node
    {
        public TValue[] Values;

        public volatile LeafNode Previous;

        public volatile LeafNode Next;

        public LeafNode(int leafSize)
        {
            Keys = new TKey[leafSize];
            Values = new TValue[leafSize];
        }

        public void Update(int position, in TKey key, in TValue value)
        {
            Keys[position] = key;
            Values[position] = value;
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

            for (int i = 0, j = position; i < rightLen; ++i, ++j)
            {
                Keys[i] = leftLeaf.Keys[j];
                Values[i] = leftLeaf.Values[j];
            }

            Next = leftLeaf.Next;
            if (Next != null)
                Next.Previous = this;
            leftLeaf.Next = this;
            Previous = leftLeaf;
        }

        public NodeIterator GetIterator(BTree<TKey, TValue> tree)
        {
            try
            {
                LockForRead();
                var keys = new TKey[Length];
                var values = new TValue[Length];
                Array.Copy(Keys, 0, keys, 0, Length);
                Array.Copy(Values, 0, values, 0, Length);
                return new NodeIterator(tree, this, keys, values);
            }
            finally
            {
                UnlockForRead();
            }
        }

        public FrozenNodeIterator GetFrozenIterator()
        {
            return new FrozenNodeIterator(this);
        }
    }
}