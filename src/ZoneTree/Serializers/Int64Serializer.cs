namespace Tenray.ZoneTree.Serializers;

public sealed class Int64Serializer : ISerializer<long>
{
    public long Deserialize(byte[] bytes)
    {
        return BitConverter.ToInt64(bytes);
    }

    public byte[] Serialize(in long entry)
    {
        return BitConverter.GetBytes(entry);
    }
}
