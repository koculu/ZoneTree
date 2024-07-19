using ZstdNet;

namespace Tenray.ZoneTree.Compression;

public static class ZstdDataCompression
{
    public static Memory<byte> Compress(Memory<byte> bytes, int level)
    {
        using var options = new CompressionOptions(level);
        using var compressor = new Compressor(options);
        return compressor.Wrap(bytes.Span).ToArray();
    }

    public static byte[] Decompress(Memory<byte> compressedBytes)
    {
        using var decompressor = new Decompressor();
        return decompressor.Unwrap(compressedBytes.Span).ToArray();
    }

    public static byte[] DecompressFast(Memory<byte> compressedBytes, int decompressedLength)
    {
        var decompressed = new byte[decompressedLength];
        using var decompressor = new Decompressor();
        decompressor.Unwrap(compressedBytes.Span, decompressed, 0);
        return decompressed;
    }
}