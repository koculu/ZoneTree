namespace Tenray.ZoneTree.Collections.BTree;

public partial class BTree<TKey, TValue>
{
    public class NodeIterator
    {
        public LeafNode Node { get; }

        public TKey[] Keys { get; }

        public TValue[] Values { get; }

        public TKey CurrentKey => Keys[CurrentIndex];

        public TValue CurrentValue => Values[CurrentIndex];

        public bool HasCurrent => CurrentIndex >= 0 && CurrentIndex < Keys.Length;

        int CurrentIndex = -1;
        
        readonly BTree<TKey, TValue> Tree;

        public NodeIterator(
            BTree<TKey, TValue> tree,
            LeafNode leafNode,
            TKey[] keys,
            TValue[] values)
        {
            Tree = tree;
            Node = leafNode;
            Keys = keys;
            Values = values;
        }

        public NodeIterator GetPreviousNodeIterator()
        {
            try
            {
                Tree.ReadLock();
                return Node.Previous?.GetIterator(Tree);
            }
            finally
            {
                Tree.ReadUnlock();
            }
        }

        public NodeIterator GetNextNodeIterator()
        {
            try
            {
                Tree.ReadLock();
                return Node.Next?.GetIterator(Tree);
            }
            finally
            {
                Tree.ReadUnlock();
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
}