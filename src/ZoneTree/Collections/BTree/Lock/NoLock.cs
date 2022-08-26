namespace Tenray.ZoneTree.Collections.BTree.Lock;

public class NoLock : ILocker
{
    public readonly static NoLock Instance = new();

    NoLock()
    {
    }

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
