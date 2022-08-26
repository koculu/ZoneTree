using System.IO.Compression;

namespace Tenray.ZoneTree.Compression;

public static class BrotliDataCompression
{
    public static byte[] Compress(Span<byte> span, int level)
    {
        using var msOutput = new MemoryStream();
        using var gzs = new BrotliStream(msOutput, (CompressionLevel)level, false);
        gzs.Write(span);
        gzs.Flush();
        return msOutput.ToArray();
    }

    public static byte[] Decompress(byte[] compressedBytes)
    {
        using var msInput = new MemoryStream(compressedBytes);
        using var msOutput = new MemoryStream();
        using var gzs = new BrotliStream(msInput, CompressionMode.Decompress);
        gzs.CopyTo(msOutput);
        var decompressed = msOutput.ToArray();
        return decompressed;
    }

    public static byte[] DecompressFast(byte[] compressedBytes, int decompressedLength)
    {
        var decompressed = new byte[decompressedLength];
        using var msInput = new MemoryStream(compressedBytes);
        using var msOutput = new MemoryStream(decompressed);
        using var gzs = new BrotliStream(msInput, CompressionMode.Decompress);
        gzs.CopyTo(msOutput);
        return decompressed;
    }
}
