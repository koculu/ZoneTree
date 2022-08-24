namespace Tenray.ZoneTree.Serializers;

public class ByteArraySerializer : ISerializer<byte[]>
{
    public byte[] Deserialize(byte[] bytes)
    {
        return bytes;
    }

    public byte[] Serialize(in byte[] entry)
    {
        return entry;
    }
}