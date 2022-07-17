using System.Text;
using ZoneTree.Core;

namespace ZoneTree.Serializers;

public class Utf8StringSerializer : ISerializer<string>
{
    public string Deserialize(byte[] bytes)
    {
        return Encoding.UTF8.GetString(bytes);
    }

    public byte[] Serialize(string entry)
    {
        return Encoding.UTF8.GetBytes(entry);
    }
}
