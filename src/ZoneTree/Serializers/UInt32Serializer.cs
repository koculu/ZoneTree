namespace Tenray.ZoneTree.Serializers;

public sealed class UInt32Serializer : ISerializer<uint>
{
    public uint Deserialize(Memory<byte> bytes)
    {
        return BitConverter.ToUInt32(bytes.Span);
    }

    public Memory<byte> Serialize(in uint entry)
    {
        return BitConverter.GetBytes(entry);
    }
}