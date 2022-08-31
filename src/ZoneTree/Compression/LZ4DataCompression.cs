using K4os.Compression.LZ4;

namespace Tenray.ZoneTree.Compression;

public static class LZ4DataCompression
{
    public static byte[] Compress(Span<byte> span, int level)
    {
        return LZ4Pickler.Pickle(span, (LZ4Level)level);
    }

    public static byte[] Compress(byte[] bytes, int level)
    {
        return LZ4Pickler.Pickle(bytes, (LZ4Level)level);
    }

    public static byte[] Decompress(byte[] compressedBytes)
    {
        return LZ4Pickler.Unpickle(compressedBytes);
    }

    public static byte[] DecompressFast(byte[] compressedBytes, int decompressedLength)
    {
        var bytes = new byte[decompressedLength];
        LZ4Pickler.Unpickle(compressedBytes, bytes);
        return bytes;
    }
}
