namespace Tenray.ZoneTree.Serializers;

public sealed class Int16Serializer : ISerializer<short>
{
    public short Deserialize(byte[] bytes)
    {
        return BitConverter.ToInt16(bytes);
    }

    public byte[] Serialize(in short entry)
    {
        return BitConverter.GetBytes(entry);
    }
}