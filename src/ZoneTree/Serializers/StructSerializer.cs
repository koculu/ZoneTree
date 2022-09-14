namespace Tenray.ZoneTree.Serializers;

public sealed class StructSerializer<TType> : ISerializer<TType> where TType : unmanaged
{
    public TType Deserialize(byte[] bytes)
    {
        return BinarySerializerHelper.FromByteArray<TType>(bytes);
    }

    public byte[] Serialize(in TType entry)
    {
        return BinarySerializerHelper.ToByteArray(entry);
    }
}