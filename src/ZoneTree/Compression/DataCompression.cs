using System.IO.Compression;

namespace Tenray.ZoneTree.Compression;

public static class DataCompression
{
    public static byte[] Compress(CompressionMethod method, Span<byte> span)
    {
        return method switch
        {
            CompressionMethod.LZ4 => LZ4DataCompression.Compress(span),
            CompressionMethod.Gzip => GZipDataCompression.Compress(span),
            CompressionMethod.None => span.ToArray(),
            _ => throw new ArgumentOutOfRangeException(nameof(method)),
        };
    }

    public static byte[] Compress(CompressionMethod method, byte[] byteArray)
    {
        return method switch
        {
            CompressionMethod.LZ4 => LZ4DataCompression.Compress(byteArray),
            CompressionMethod.Gzip => GZipDataCompression.Compress(byteArray),
            CompressionMethod.None => byteArray,
            _ => throw new ArgumentOutOfRangeException(nameof(method)),
        };
    }

    public static byte[] Decompress(
        CompressionMethod method, byte[] compressedBytes)
    {
        return method switch
        {
            CompressionMethod.LZ4 => LZ4DataCompression.Decompress(compressedBytes),
            CompressionMethod.Gzip => GZipDataCompression.Decompress(compressedBytes),
            CompressionMethod.None => compressedBytes,            
            _ => throw new ArgumentOutOfRangeException(nameof(method)),
        };
    }

    public static byte[] DecompressFast(
        CompressionMethod method, byte[] compressedBytes, int decompressedLength)
    {
        return method switch
        {
            CompressionMethod.LZ4 => LZ4DataCompression.DecompressFast(compressedBytes, decompressedLength),
            CompressionMethod.Gzip => GZipDataCompression.DecompressFast(compressedBytes, decompressedLength),
            CompressionMethod.None => compressedBytes,
            _ => throw new ArgumentOutOfRangeException(nameof(method)),
        };
    }
}
