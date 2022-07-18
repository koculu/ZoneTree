namespace ZoneTree.Core;

public class IncrementalInt64IdProvider
{
    long lastId = 0;
    public long NextId()
    {
        return Interlocked.Increment(ref lastId);
    }

    public void SetNextId(long id)
    {
        Interlocked.Exchange(ref lastId, id - 1);
    }
}