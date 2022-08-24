using System.IO.Compression;

namespace Tenray.ZoneTree.Compression;

public static class DataCompression
{
    public static byte[] Compress(CompressionMethod method, Span<byte> span)
    {
        return method switch
        {
            CompressionMethod.Gzip => GZipDataCompression.Compress(span),
            CompressionMethod.None => span.ToArray(),
            _ => throw new ArgumentOutOfRangeException(nameof(method)),
        };
    }

    public static byte[] Compress(CompressionMethod method, byte[] byteArray)
    {
        return method switch
        {
            CompressionMethod.Gzip => GZipDataCompression.Compress(byteArray),
            CompressionMethod.None => byteArray,
            _ => throw new ArgumentOutOfRangeException(nameof(method)),
        };
    }

    public static byte[] Decompress(CompressionMethod method, byte[] compressedBytes)
    {
        return method switch
        {
            CompressionMethod.Gzip => GZipDataCompression.Decompress(compressedBytes),
            CompressionMethod.None => compressedBytes,
            _ => throw new ArgumentOutOfRangeException(nameof(method)),
        };
    }
}
