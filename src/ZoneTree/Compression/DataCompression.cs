using Tenray.ZoneTree.Options;

namespace Tenray.ZoneTree.Compression;

public static class DataCompression
{
    public static Memory<byte> Compress(CompressionMethod method, int level, Memory<byte> bytes)
    {
        return method switch
        {
            CompressionMethod.LZ4 => LZ4DataCompression.Compress(bytes, level),
            CompressionMethod.Zstd => ZstdDataCompression.Compress(bytes, level),
            CompressionMethod.Brotli => BrotliDataCompression.Compress(bytes, level),
            CompressionMethod.Gzip => GZipDataCompression.Compress(bytes, level),
            CompressionMethod.None => bytes,
            _ => throw new ArgumentOutOfRangeException(nameof(method)),
        };
    }

    public static Memory<byte> Decompress(
        CompressionMethod method, Memory<byte> compressedBytes)
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

    public static Memory<byte> DecompressFast(
        CompressionMethod method, Memory<byte> compressedBytes, int decompressedLength)
    {
        return method switch
        {
            CompressionMethod.LZ4 => LZ4DataCompression.DecompressFast(compressedBytes, decompressedLength),
            CompressionMethod.Zstd => ZstdDataCompression.DecompressFast(compressedBytes, decompressedLength),
            CompressionMethod.Brotli => BrotliDataCompression.DecompressFast(compressedBytes, decompressedLength),
            CompressionMethod.Gzip => GZipDataCompression.DecompressFast(compressedBytes, decompressedLength),
            CompressionMethod.None => compressedBytes.ToArray(),
            _ => throw new ArgumentOutOfRangeException(nameof(method)),
        };
    }
}
