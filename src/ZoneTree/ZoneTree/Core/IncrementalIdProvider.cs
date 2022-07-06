namespace ZoneTree.Core;

public class IncrementalIdProvider : IIncrementalIdProvider
{
    int nextId = 0;
    public int NextId()
    {
        return Interlocked.Increment(ref nextId);
    }

    public void SetNextId(int id)
    {
        Interlocked.Exchange(ref nextId, id - 1);
    }
}