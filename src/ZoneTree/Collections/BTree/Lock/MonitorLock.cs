namespace Tenray.ZoneTree.Collections.BTree.Lock;

public sealed class MonitorLock : ILocker
{
    public void WriteLock()
    {
        Monitor.Enter(this);
    }

    public void WriteUnlock()
    {
        Monitor.Exit(this);
    }

    public void ReadLock()
    {
        Monitor.Enter(this);
    }

    public void ReadUnlock()
    {
        Monitor.Exit(this);
    }

    public bool TryEnterWriteLock(int millisecondsTimeout)
    {
        return Monitor.TryEnter(this, millisecondsTimeout);
    }
}
