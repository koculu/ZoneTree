namespace Tenray.ZoneTree.Serializers;

public sealed class DecimalSerializer : ISerializer<decimal>
{
    public decimal Deserialize(byte[] bytes)
    {
        return BinarySerializerHelper.FromByteArray<decimal>(bytes);
    }

    public byte[] Serialize(in decimal entry)
    {
        return BinarySerializerHelper.ToByteArray(entry);
    }
}
