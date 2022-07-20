using System.Runtime.InteropServices;

namespace Tenray.ZoneTree.Serializers;

[StructLayout(LayoutKind.Sequential)]
public struct CombinedValue<TValue1, TValue2>
{
    public TValue1 Value1;

    public TValue2 Value2;

    public CombinedValue(in TValue1 value1, in TValue2 value2) : this()
    {
        Value1 = value1;
        Value2 = value2;
    }
}
