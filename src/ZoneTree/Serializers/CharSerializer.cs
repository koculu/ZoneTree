
namespace Tenray.ZoneTree.Serializers;

public sealed class CharSerializer : ISerializer<char>
{
    public char Deserialize(Memory<byte> bytes)
    {
        return (char)bytes.Span[0];
    }

    public Memory<byte> Serialize(in char entry)
    {
        return new byte[1] { (byte)entry };
    }
}