namespace Tenray.ZoneTree.Serializers;

public sealed class Int32Serializer : ISerializer<int>
{
    public int Deserialize(Memory<byte> bytes)
    {
        return BitConverter.ToInt32(bytes.Span);
    }

    public Memory<byte> Serialize(in int entry)
    {
        return BitConverter.GetBytes(entry);
    }
}
