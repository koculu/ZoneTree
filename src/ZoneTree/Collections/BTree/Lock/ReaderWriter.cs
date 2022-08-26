namespace Tenray.ZoneTree.Collections.BTree.Lock;

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