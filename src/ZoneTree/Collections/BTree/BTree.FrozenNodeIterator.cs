﻿using Tenray.ZoneTree.Comparers;

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
            return Node.Previous?.CreateFrozenIterator();
        }

        public FrozenNodeIterator GetNextNodeIterator()
        {
            return Node.Next?.CreateFrozenIterator();
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
        /// <param name="comparer">The key comparer</param>
        /// <param name="key">The key</param>
        /// <returns>-1 or a valid position</returns>
        public int GetLastSmallerOrEqualPosition(
            IRefComparer<TKey> comparer, in TKey key)
            => BinarySearchAlgorithms
                .LastSmallerOrEqualPosition(Node.Keys, 0, Node.Length - 1, comparer, in key);

        /// <summary>
        /// Finds the position of element that is greater or equal than key.
        /// </summary>
        /// <param name="comparer">The key comparer</param>
        /// <param name="key">The key</param>
        /// <returns>The length of the segment or a valid position</returns>
        public int GetFirstGreaterOrEqualPosition(
            IRefComparer<TKey> comparer, in TKey key)
            => BinarySearchAlgorithms
                .FirstGreaterOrEqualPosition(Node.Keys, 0, Node.Length - 1, comparer, in key);
    }
}