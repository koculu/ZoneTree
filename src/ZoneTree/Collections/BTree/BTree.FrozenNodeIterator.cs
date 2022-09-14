using Tenray.ZoneTree.Comparers;

namespace Tenray.ZoneTree.Collections.BTree;

public sealed partial class BTree<TKey, TValue>
{
    public sealed class FrozenNodeIterator
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