using Tenray.ZoneTree.Core;

namespace Tenray.ZoneTree.Serializers;

public class Int64Serializer : ISerializer<long>
{
    public long Deserialize(byte[] bytes)
    {
        return BitConverter.ToInt32(bytes);
    }

    public byte[] Serialize(long entry)
    {
        return BitConverter.GetBytes(entry);
    }
}
