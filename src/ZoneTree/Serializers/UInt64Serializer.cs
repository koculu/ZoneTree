namespace Tenray.ZoneTree.Serializers;

public sealed class UInt64Serializer : ISerializer<ulong>
{
    public ulong Deserialize(Memory<byte> bytes)
    {
        return BitConverter.ToUInt64(bytes.Span);
    }

    public Memory<byte> Serialize(in ulong entry)
    {
        return BitConverter.GetBytes(entry);
    }
}
