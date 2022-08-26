using ZstdSharp;

namespace Tenray.ZoneTree.Compression;

public static class ZstdDataCompression
{
    public static byte[] Compress(Span<byte> span)
    {
        using var compressor = new Compressor();
        return compressor.Wrap(span).ToArray();
    }

    public static byte[] Decompress(byte[] compressedBytes)
    {
        using var decompressor = new Decompressor();
        return decompressor.Unwrap(compressedBytes).ToArray();
    }

    public static byte[] DecompressFast(byte[] compressedBytes, int decompressedLength)
    {
        var decompressed = new byte[decompressedLength];
        using var decompressor = new Decompressor();
        decompressor.Unwrap(compressedBytes, decompressed, 0);
        return decompressed;
    }
}