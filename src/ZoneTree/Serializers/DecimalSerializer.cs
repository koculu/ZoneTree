
namespace Tenray.ZoneTree.Serializers;

public sealed class DecimalSerializer : ISerializer<decimal>
{
    public decimal Deserialize(Memory<byte> bytes)
    {
        return BinarySerializerHelper.FromByteArray<decimal>(bytes);
    }

    public Memory<byte> Serialize(in decimal entry)
    {
        return BinarySerializerHelper.ToByteArray(entry);
    }
}
