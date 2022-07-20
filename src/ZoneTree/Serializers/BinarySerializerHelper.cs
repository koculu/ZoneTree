using System.Runtime.CompilerServices;

namespace Tenray.ZoneTree.Serializers;

public static class BinarySerializerHelper
{
    public static unsafe byte[] ToByteArray<T>(in T value) where T : unmanaged
    {
        byte[] result = new byte[sizeof(T)];
        Unsafe.As<byte, T>(ref result[0]) = value;
        return result;
    }

    public static T FromByteArray<T>(byte[] data) where T : unmanaged
        => Unsafe.As<byte, T>(ref data[0]);

    public static T FromByteArray<T>(byte[] data, int off) where T : unmanaged
        => Unsafe.As<byte, T>(ref data[off]);
}
