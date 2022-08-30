#undef USE_NODE_IDS

namespace Tenray.ZoneTree.Collections.BTree;

public interface ILocker
{
    bool TryEnterWriteLock(int millisecondsTimeout);

    void WriteLock();

    void WriteUnlock();

    void ReadLock();

    void ReadUnlock();
}
