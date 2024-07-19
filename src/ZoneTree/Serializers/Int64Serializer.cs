namespace Tenray.ZoneTree.Serializers;

public sealed class Int64Serializer : ISerializer<long>
{
    public long Deserialize(Memory<byte> bytes)
    {
        return BitConverter.ToInt64(bytes.Span);
    }

    public Memory<byte> Serialize(in long entry)
    {
        return BitConverter.GetBytes(entry);
    }
}
