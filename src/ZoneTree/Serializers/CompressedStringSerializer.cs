using System.IO.Compression;
using System.Text;
using ZoneTree.Core;

namespace ZoneTree.Serializers;

public class CompressedStringSerializer : ISerializer<string>
{
    public string Deserialize(byte[] bytes)
    {
        return FromGzip(bytes);
    }

    public byte[] Serialize(string entry)
    {
        return ToGzip(entry);
    }

    public static byte[] ToGzip(string value)
    {
        CompressionLevel level = CompressionLevel.Fastest;
        var bytes = Encoding.UTF8.GetBytes(value);
        using var input = new MemoryStream(bytes);
        using var output = new MemoryStream();
        using var stream = new GZipStream(output, level);
        input.CopyTo(stream);
        return output.ToArray();
    }

    public static string FromGzip(byte[] bytes)
    {
        using var input = new MemoryStream(bytes);
        using var output = new MemoryStream();
        using var stream = new GZipStream(input, CompressionMode.Decompress);

        stream.CopyTo(output);
        stream.Flush();

        return Encoding.UTF8.GetString(output.ToArray());
    }
}
