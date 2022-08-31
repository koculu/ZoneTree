using Tenray.ZoneTree.Options;

namespace Tenray.ZoneTree.Compression;

public static class DataCompression
{
    public static byte[] Compress(CompressionMethod method, int level, Span<byte> span)
    {
        return method switch
        {
            CompressionMethod.LZ4 => LZ4DataCompression.Compress(span, level),
            CompressionMethod.Zstd => ZstdDataCompression.Compress(span, level),
            CompressionMethod.Brotli => BrotliDataCompression.Compress(span, level),
            CompressionMethod.Gzip => GZipDataCompression.Compress(span, level),
            CompressionMethod.None => span.ToArray(),
            _ => throw new ArgumentOutOfRangeException(nameof(method)),
        };
    }

    public static byte[] Compress(CompressionMethod method, int level, byte[] byteArray)
    {
        return method switch
        {
            CompressionMethod.LZ4 => LZ4DataCompression.Compress(byteArray, level),
            CompressionMethod.Zstd => ZstdDataCompression.Compress(byteArray, level),
            CompressionMethod.Brotli => BrotliDataCompression.Compress(byteArray, level),
            CompressionMethod.Gzip => GZipDataCompression.Compress(byteArray, level),
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
            CompressionMethod.Zstd => ZstdDataCompression.Decompress(compressedBytes),
            CompressionMethod.Brotli => BrotliDataCompression.Decompress(compressedBytes),
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
            CompressionMethod.Zstd => ZstdDataCompression.DecompressFast(compressedBytes, decompressedLength),
            CompressionMethod.Brotli => BrotliDataCompression.DecompressFast(compressedBytes, decompressedLength),
            CompressionMethod.Gzip => GZipDataCompression.DecompressFast(compressedBytes, decompressedLength),
            CompressionMethod.None => compressedBytes,
            _ => throw new ArgumentOutOfRangeException(nameof(method)),
        };
    }
}
