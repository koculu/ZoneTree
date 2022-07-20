using System.Runtime.InteropServices;

namespace Tenray.ZoneTree.Transactional;

[StructLayout(LayoutKind.Sequential)]
public struct ReadWriteStamp
{
    public long ReadStamp;

    public long WriteStamp;

    public bool IsDeleted => ReadStamp == 0 && WriteStamp == 0;
}
