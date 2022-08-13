#undef USE_NODE_IDS

using Tenray.ZoneTree.Collections.BplusTree.Lock;

namespace Tenray.ZoneTree.Collections.BTree;

public partial class BTree<TKey, TValue>
{
    public class Node
    {
        readonly ILocker Locker;

        public TKey[] Keys;

        public Node[] Children;

        public int Length = 0;

        public bool IsFull => Keys.Length == Length;

#if USE_NODE_IDS
        static int IncrementalId = 0;
      
        public int Id;
#endif

        public Node(ILocker locker)
        {
            Locker = locker;
#if USE_NODE_IDS
            Id = Interlocked.Increment(ref IncrementalId);
#endif
        }

        public Node(ILocker locker, int nodeSize)
        {
            Locker = locker;
#if USE_NODE_IDS
            Id = Interlocked.Increment(ref IncrementalId);
#endif
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

        public void InsertKeyAndChild(int position, in TKey key, Node left, Node right)
        {
            var len = Length - position;
            if (len > 0)
            {
                Array.Copy(Keys, position, Keys, position + 1, len);
                Array.Copy(Children, position + 1, Children, position + 2, len);
            }
            Keys[position] = key;
            Children[position] = left;
            Children[position + 1] = right;
            ++Length;
        }

        public void ReadLock()
        {
            Locker.ReadLock();
        }

        public void ReadUnlock()
        {
            Locker.ReadUnlock();
        }

        public void WriteLock()
        {
            Locker.WriteLock();
        }

        public void WriteUnlock()
        {
            Locker.WriteUnlock();
        }

        public (Node left, Node right) Split(
            int middle, int nodeSize, ILocker locker1, ILocker locker2)
        {
            var left = new Node(locker1, nodeSize);
            var right = new Node(locker2, nodeSize);
            left.Length = middle;
            right.Length = Length - middle;
            Array.Copy(Keys, 0, left.Keys, 0, middle);
            Array.Copy(Children, 0, left.Children, 0, middle+1);
            Array.Copy(Keys, middle, right.Keys, 0, right.Length);
            Array.Copy(Children, middle, right.Children, 0, right.Length+1);
            return (left, right);
        }
    }
}
