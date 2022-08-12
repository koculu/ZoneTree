#undef USE_NODE_IDS

using Tenray.ZoneTree.Collections.BTree;

namespace Tenray.ZoneTree.Collections.BplusTree.Lock;

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
