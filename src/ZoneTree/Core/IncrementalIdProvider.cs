namespace Tenray.ZoneTree.Core;

public sealed class IncrementalIdProvider : IIncrementalIdProvider
{
    long lastId = 0;

    public long LastId => Volatile.Read(ref lastId);

    public long NextId()
    {
        return Interlocked.Increment(ref lastId);
    }

    public void SetNextId(long id)
    {
        Interlocked.Exchange(ref lastId, id - 1);
    }
}
