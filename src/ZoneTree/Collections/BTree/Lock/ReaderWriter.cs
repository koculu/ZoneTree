namespace Tenray.ZoneTree.Collections.BTree.Lock;

public sealed class ReadWriteLock : ILocker
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

    public bool TryEnterWriteLock(int millisecondsTimeout)
    {
        return Locker.TryEnterWriteLock(millisecondsTimeout);
    }
}