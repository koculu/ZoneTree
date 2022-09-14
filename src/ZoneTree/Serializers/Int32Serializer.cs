namespace Tenray.ZoneTree.Serializers;

public sealed class Int32Serializer : ISerializer<int>
{
    public int Deserialize(byte[] bytes)
    {
        return BitConverter.ToInt32(bytes);
    }

    public byte[] Serialize(in int entry)
    {
        return BitConverter.GetBytes(entry);
    }
}
