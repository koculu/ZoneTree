using System.Runtime.InteropServices;

namespace ZoneTree.Transactional;

[StructLayout(LayoutKind.Sequential)]
public struct TransactionMeta
{
    public TransactionState State;

    public long StartedAt;

    public long EndedAt;
}
