using Tenray.LZ4;

namespace Tenray.ZoneTree.Compression;

public static class LZ4DataCompression
{
    const int BlockSize = 1024 * 32 * 8;

    public static byte[] Compress(Span<byte> span, int level)
    {
        using var msOutput = new MemoryStream();
        using var gzs = new LZ4Stream(msOutput,
            CompressionMode.Compress, true, level == 1, BlockSize);
        gzs.Write(span);
        gzs.Flush();
        return msOutput.ToArray();
    }

    public static byte[] Decompress(byte[] compressedBytes)
    {
        using var msInput = new MemoryStream(compressedBytes);
        using var msOutput = new MemoryStream();
        using var gzs = new LZ4Stream(
            msInput, CompressionMode.Decompress, true, false, BlockSize);
        gzs.CopyTo(msOutput);
        var decompressed = msOutput.ToArray();
        return decompressed;
    }

    public static byte[] DecompressFast(byte[] compressedBytes, int decompressedLength)
    {
        var decompressed = new byte[decompressedLength];
        using var msInput = new MemoryStream(compressedBytes);
        using var msOutput = new MemoryStream(decompressed);
        using var gzs = new LZ4Stream(
            msInput, CompressionMode.Decompress, true, false, BlockSize);
        gzs.CopyTo(msOutput);
        return decompressed;
    }
}
