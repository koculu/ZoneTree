
namespace Tenray.ZoneTree.Serializers;

public sealed class ByteSerializer : ISerializer<byte>
{
    public byte Deserialize(Memory<byte> bytes)
    {
        return bytes.Span[0];
    }

    public Memory<byte> Serialize(in byte entry)
    {
        return new byte[1] { entry };
    }
}
