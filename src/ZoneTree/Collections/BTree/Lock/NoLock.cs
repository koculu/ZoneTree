using Tenray.ZoneTree.Collections.BTree;

namespace Tenray.ZoneTree.Collections.BTree.Lock;

public class NoLock : ILocker
{
    public void WriteLock()
    {
    }

    public void WriteUnlock()
    {
    }

    public void ReadLock()
    {
    }

    public void ReadUnlock()
    {
    }
}
