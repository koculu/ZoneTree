using System.IO.Compression;
using System.Text;
using Tenray.ZoneTree.Core;

namespace Tenray.ZoneTree.Serializers;

public class CompressedStringSerializer : ISerializer<string>
{
    public string Deserialize(byte[] bytes)
    {
        return FromGzip(bytes);
    }

    public byte[] Serialize(in string entry)
    {
        return ToGzip(entry);
    }

    public static byte[] ToGzip(string value)
    {
        CompressionLevel level = CompressionLevel.Fastest;
        var bytes = Encoding.UTF8.GetBytes(value);
        using var output = new MemoryStream();
        using var stream = new GZipStream(output, level);
        stream.Write(bytes);
        stream.Flush();
        return output.ToArray();
    }

    public static string FromGzip(byte[] bytes)
    {
        using var input = new MemoryStream(bytes);
        using var output = new MemoryStream();
        using var stream = new GZipStream(input, CompressionMode.Decompress);
        stream.CopyTo(output);
        return Encoding.UTF8.GetString(output.ToArray());
    }
}
