namespace Tenray.ZoneTree.Serializers;

public sealed class NullableInt32Serializer : ISerializer<int?>
{
    public int? Deserialize(Memory<byte> bytes)
    {
        if (bytes.Length == 0)
            return null;
        return BitConverter.ToInt32(bytes.Span);
    }

    public Memory<byte> Serialize(in int? entry)
    {
        if (entry.HasValue)
            return BitConverter.GetBytes(entry.Value);
        return Array.Empty<byte>();
    }
}
