namespace ZoneTree.Core;

public class IncrementalIdProvider : IIncrementalIdProvider
{
    int lastId = 0;
    public int NextId()
    {
        return Interlocked.Increment(ref lastId);
    }

    public void SetNextId(int id)
    {
        Interlocked.Exchange(ref lastId, id - 1);
    }
}