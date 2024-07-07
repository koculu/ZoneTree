namespace Tenray.ZoneTree.Serializers;

public sealed class BooleanSerializer : ISerializer<bool>
{
    public bool Deserialize(Memory<byte> bytes)
    {
        return BitConverter.ToBoolean(bytes.Span);
    }

    public Memory<byte> Serialize(in bool entry)
    {
        return BitConverter.GetBytes(entry);
    }
}
