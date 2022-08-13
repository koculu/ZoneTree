using System.Text;
using Tenray.ZoneTree.Core;

namespace Tenray.ZoneTree.Serializers;

public class UnicodeStringSerializer : ISerializer<string>
{
    public string Deserialize(byte[] bytes)
    {
        return Encoding.Unicode.GetString(bytes);
    }

    public byte[] Serialize(in string entry)
    {
        return Encoding.Unicode.GetBytes(entry);
    }
}
