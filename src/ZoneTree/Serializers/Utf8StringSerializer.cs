using System.Text;
using Tenray.ZoneTree.Core;

namespace Tenray.ZoneTree.Serializers;

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
