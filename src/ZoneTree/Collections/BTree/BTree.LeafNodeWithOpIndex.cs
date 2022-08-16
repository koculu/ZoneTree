namespace Tenray.ZoneTree.Collections.BTree;

public partial class BTree<TKey, TValue>
{
    public class LeafNodeWithOpIndex : LeafNode
    {
        public long[] OpIndexes;

        public LeafNodeWithOpIndex(ILocker locker, int leafSize) : base(locker, leafSize)
        {
            OpIndexes = new long[leafSize];
        }

        public override void Update(int position, in TKey key, in TValue value, long opIndex)
        {
            Keys[position] = key;
            Values[position] = value;
            OpIndexes[position] = opIndex;
        }

        public override void Insert(int position, in TKey key, in TValue value, long opIndex)
        {
            var len = Length - position;
            if (len > 0)
            {
                Array.Copy(Keys, position, Keys, position + 1, len);
                Array.Copy(Values, position, Values, position + 1, len);
                Array.Copy(OpIndexes, position, OpIndexes, position + 1, len);
            }
            Keys[position] = key;
            Values[position] = value;
            OpIndexes[position] = opIndex;
            ++Length;
        }

        public override NodeIterator GetIterator(BTree<TKey, TValue> tree)
        {
            try
            {
                ReadLock();
                var keys = new TKey[Length];
                var values = new TValue[Length];
                var opIndexes = new long[Length];
                Array.Copy(Keys, 0, keys, 0, Length);
                Array.Copy(Values, 0, values, 0, Length);
                Array.Copy(OpIndexes, 0, opIndexes, 0, Length);
                return new NodeIterator(tree, this, keys, values, opIndexes);
            }
            finally
            {
                ReadUnlock();
            }
        }

        public override (LeafNode left, LeafNode right) SplitLeaf(
            int middle,
            int leafSize, ILocker locker1, ILocker locker2)
        {
            var left = new LeafNodeWithOpIndex(locker1, leafSize);
            var right = new LeafNodeWithOpIndex(locker2, leafSize);
            left.Length = middle;
            right.Length = Length - middle;
            Array.Copy(Keys, 0, left.Keys, 0, middle);
            Array.Copy(Values, 0, left.Values, 0, middle);
            Array.Copy(OpIndexes, 0, left.OpIndexes, 0, middle);
            Array.Copy(Keys, middle, right.Keys, 0, right.Length);
            Array.Copy(Values, middle, right.Values, 0, right.Length);
            Array.Copy(OpIndexes, middle, right.OpIndexes, 0, right.Length);

            left.Next = right;
            right.Previous = left;

            return (left, right);
        }
    }
}