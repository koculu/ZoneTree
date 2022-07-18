using System.Runtime.InteropServices;

namespace ZoneTree.Transactional;

[StructLayout(LayoutKind.Sequential)]
public struct ReadWriteStamp
{
    public long ReadStamp;

    public long WriteStamp;

    public bool IsDeleted => ReadStamp == 0 && WriteStamp == 0;
}
