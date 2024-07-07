using System.IO.Compression;
using Tenray.ZoneTree.AbstractFileStream;

namespace Tenray.ZoneTree.Compression;

public static class BrotliDataCompression
{
    public static Memory<byte> Compress(Memory<byte> bytes, int level)
    {
        // Brotli Optimum level is extremely slow.
        // Changing the optimum level to fastest!
        if (level == 0)
            level = 1;
        using var msOutput = new MemoryStream();
        using var gzs = new BrotliStream(msOutput, (CompressionLevel)level, false);
        gzs.Write(bytes.Span);
        gzs.Flush();
        return msOutput.ToArray();
    }

    public static byte[] Decompress(Memory<byte> compressedBytes)
    {
        using var pin = compressedBytes.Pin();
        using var msInput = compressedBytes.ToReadOnlyStream(pin);
        using var msOutput = new MemoryStream();
        using var gzs = new BrotliStream(msInput, CompressionMode.Decompress);
        gzs.CopyTo(msOutput);
        var decompressed = msOutput.ToArray();
        return decompressed;
    }

    public static byte[] DecompressFast(Memory<byte> compressedBytes, int decompressedLength)
    {
        var decompressed = new byte[decompressedLength];
        using var pin = compressedBytes.Pin();
        using var msInput = compressedBytes.ToReadOnlyStream(pin);
        using var msOutput = new MemoryStream(decompressed);
        using var gzs = new BrotliStream(msInput, CompressionMode.Decompress);
        gzs.CopyTo(msOutput);
        return decompressed;
    }
}
