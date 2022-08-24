using System.Runtime.InteropServices;

namespace Tenray.ZoneTree.PresetTypes;

[StructLayout(LayoutKind.Sequential)]
public struct Deletable<TValue>
{
    public TValue Value;

    public bool IsDeleted;

    public Deletable(in TValue value, bool isDeleted = false) : this()
    {
        Value = value;
        IsDeleted = isDeleted;
    }
}
