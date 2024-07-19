using System.Text;

namespace Tenray.ZoneTree.Serializers;

public sealed class UnicodeStringSerializer : ISerializer<string>
{
    public string Deserialize(Memory<byte> bytes)
    {
        if (bytes.Length == 1)
            return null;
        return Encoding.Unicode.GetString(bytes.Span);
    }

    public string Deserialize(Span<byte> bytes)
    {
        if (bytes.Length == 1)
            return null;
        return Encoding.Unicode.GetString(bytes);
    }

    public Memory<byte> Serialize(in string entry)
    {
        if (entry == null)
            return new byte[1] { 0 };
        return Encoding.Unicode.GetBytes(entry);
    }
}
