using Tenray.LZ4;
using Tenray.LZ4.Core;

namespace Tenray.ZoneTree.Compression;

public static class LZ4DataCompression
{
    const int BlockSize = 1024 * 32 * 8;

    public static byte[] Compress(Span<byte> span)
    {
        using var msOutput = new MemoryStream();
        using var gzs = new LZ4Stream(msOutput,
            CompressionMode.Compress, true, false, BlockSize);
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
}
