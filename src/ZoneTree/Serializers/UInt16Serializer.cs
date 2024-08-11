namespace Tenray.ZoneTree.Serializers;

public sealed class UInt16Serializer : ISerializer<ushort>
{
    public ushort Deserialize(Memory<byte> bytes)
    {
        return BitConverter.ToUInt16(bytes.Span);
    }

    public Memory<byte> Serialize(in ushort entry)
    {
        return BitConverter.GetBytes(entry);
    }
}