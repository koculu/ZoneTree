using System.Text;

namespace Tenray.ZoneTree.Serializers;

public sealed class Utf8StringSerializer : ISerializer<string>
{
    // Single byte 0xC2 is not a valid UTF-8 string.
    // We can use that to serialize the null strings.
    const byte NullMarker = 0xC2;

    public string Deserialize(byte[] bytes)
    {
        if (bytes.Length == 1 && bytes[0] == 0xC2)
            return null;
        return Encoding.UTF8.GetString(bytes);
    }

    public byte[] Serialize(in string entry)
    {
        if (entry == null)
        {
            return new byte[1] { NullMarker };
        }
        return Encoding.UTF8.GetBytes(entry);
    }
}
