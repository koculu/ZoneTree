namespace ZoneTree.Core;

public interface IIncrementalIdProvider
{
    int NextId();

    void SetNextId(int id);
}
