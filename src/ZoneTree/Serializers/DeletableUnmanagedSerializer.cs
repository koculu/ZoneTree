using Tenray.ZoneTree.Core;

namespace Tenray.ZoneTree.Serializers;

public class DeletableUnmanagedSerializer<TValue> : ISerializer<Deletable<TValue>>
    where TValue : unmanaged
{
    public Deletable<TValue> Deserialize(byte[] bytes)
    {
        return BinarySerializerHelper.FromByteArray<Deletable<TValue>>(bytes);
    }

    public byte[] Serialize(in Deletable<TValue> entry)
    {
        return BinarySerializerHelper.ToByteArray(entry);
    }
}