#undef USE_NODE_IDS

namespace Tenray.ZoneTree.Collections.BTree;

public partial class BTree<TKey, TValue>
{
    public class Node
    {
        readonly MonitorLock Locker = new();

        public TKey[] Keys;

        public Node[] Children;

        public int Length = 0;

        public bool IsFull => Keys.Length == Length;

#if USE_NODE_IDS
        static int IncrementalId = 0;
      
        public int Id;
#endif

        public Node()
        {
#if USE_NODE_IDS
            Id = Interlocked.Increment(ref IncrementalId);
#endif
        }

        public Node(int nodeSize)
        {
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

        public void LockForRead()
        {
            Locker.SharedLock();
        }

        public void UnlockForRead()
        {
            Locker.SharedUnlock();
        }

        public void LockForWrite()
        {
            Locker.Lock();
        }

        public void UnlockForWrite()
        {
            Locker.Unlock();
        }
    }
}

public class NoLock
{
    public void Lock()
    {
    }

    public void SharedLock()
    {
    }

    public void Unlock()
    {
    }

    public void SharedUnlock()
    {
    }
}

public class TicketLock
{
    volatile int locked = 0;
    volatile int unlocked = 0;
    volatile int lastLockedThreadId = 0;
    public void Lock()
    {
        var id = Environment.CurrentManagedThreadId;
        try
        {
            var myTicket = Interlocked.Increment(ref locked);
            if (id == lastLockedThreadId)
                return;
            if (myTicket == 1)
                return;
            --myTicket;
            var spinWait = new SpinWait();
            while (myTicket != unlocked)
                spinWait.SpinOnce();
        }
        finally
        {
            lastLockedThreadId = id;
        }
    }

    public void SharedLock()
    {
        Lock();
    }

    public void Unlock()
    {
        var l = locked;
        if (Interlocked.Increment(ref unlocked) > l)
            throw new Exception("unlocked > locked");
    }

    public void SharedUnlock()
    {
        var l = locked;
        if (Interlocked.Increment(ref unlocked) > l)
            throw new Exception("unlocked > locked");
    }
}

public class MonitorLock
{
    public void Lock()
    {
        Monitor.Enter(this);
    }

    public void SharedLock()
    {
        Monitor.Enter(this);
    }

    public void Unlock()
    {
        Monitor.Exit(this);
    }

    public void SharedUnlock()
    {
        Monitor.Exit(this);
    }
}

public class ReaderWriterLock
{
    readonly ReaderWriterLockSlim Locker = new(LockRecursionPolicy.SupportsRecursion);
    public void Lock()
    {
        Locker.EnterWriteLock();
    }

    public void SharedLock()
    {
        Locker.EnterReadLock();
    }

    public void Unlock()
    {
        Locker.ExitWriteLock();
    }

    public void SharedUnlock()
    {
        Locker.ExitReadLock();
    }
}