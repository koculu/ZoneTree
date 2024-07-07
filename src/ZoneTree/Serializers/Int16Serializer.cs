namespace Tenray.ZoneTree.Serializers;

public sealed class Int16Serializer : ISerializer<short>
{
    public short Deserialize(Memory<byte> bytes)
    {
        return BitConverter.ToInt16(bytes.Span);
    }

    public Memory<byte> Serialize(in short entry)
    {
        return BitConverter.GetBytes(entry);
    }
}