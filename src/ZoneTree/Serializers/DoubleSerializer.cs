namespace Tenray.ZoneTree.Serializers;

public sealed class DoubleSerializer : ISerializer<double>
{
    public double Deserialize(byte[] bytes)
    {
        return BitConverter.ToDouble(bytes);
    }

    public byte[] Serialize(in double entry)
    {
        return BitConverter.GetBytes(entry);
    }
}