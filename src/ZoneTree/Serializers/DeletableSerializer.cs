using Tenray.ZoneTree.PresetTypes;

namespace Tenray.ZoneTree.Serializers;

public sealed class DeletableSerializer<TValue> : ISerializer<Deletable<TValue>>
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
