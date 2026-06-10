namespace ZoneTree.Collections.BTree.Lock;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2002:Do not lock on objects with weak identity", Justification = "Best memory footprint for BTree nodes. Avoid allocation of a new lock object per BTree node.")]
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
