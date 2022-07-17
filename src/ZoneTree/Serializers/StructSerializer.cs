using ZoneTree.Core;

namespace ZoneTree.Serializers;

public class StructSerializer<TType> : ISerializer<TType> where TType : unmanaged
{
    public TType Deserialize(byte[] bytes)
    {
        return BinarySerializerHelper.FromByteArray<TType>(bytes);
    }

    public byte[] Serialize(TType entry)
    {
        return BinarySerializerHelper.ToByteArray(entry);
    }
}
