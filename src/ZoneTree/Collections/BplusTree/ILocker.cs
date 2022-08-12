#undef USE_NODE_IDS

namespace Tenray.ZoneTree.Collections.BTree;

public interface ILocker
{
    void WriteLock();

    void WriteUnlock();

    void ReadLock();

    void ReadUnlock();
}
