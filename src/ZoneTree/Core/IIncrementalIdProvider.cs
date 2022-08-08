namespace Tenray.ZoneTree.Core;

public interface IIncrementalIdProvider
{
    long NextId();

    void SetNextId(long id);
}
