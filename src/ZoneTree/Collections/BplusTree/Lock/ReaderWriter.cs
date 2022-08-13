using Tenray.ZoneTree.Collections.BTree;

namespace Tenray.ZoneTree.Collections.BplusTree.Lock;

public class ReadWriteLock : ILocker
{
    readonly ReaderWriterLockSlim Locker = new(LockRecursionPolicy.SupportsRecursion);
    
    public void WriteLock()
    {
        Locker.EnterWriteLock();
    }

    public void ReadLock()
    {
        Locker.EnterReadLock();
    }

    public void WriteUnlock()
    {
        Locker.ExitWriteLock();
    }

    public void ReadUnlock()
    {
        Locker.ExitReadLock();
    }
}