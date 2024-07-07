namespace Tenray.ZoneTree.Serializers;

public sealed class DoubleSerializer : ISerializer<double>
{
    public double Deserialize(Memory<byte> bytes)
    {
        return BitConverter.ToDouble(bytes.Span);
    }

    public Memory<byte> Serialize(in double entry)
    {
        return BitConverter.GetBytes(entry);
    }
}