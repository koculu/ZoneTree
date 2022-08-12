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
    }
}
