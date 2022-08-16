using Tenray.ZoneTree.Collections.BTree;

namespace Tenray.ZoneTree.Collections.BTree.Lock;

public class MonitorLock : ILocker
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
}
