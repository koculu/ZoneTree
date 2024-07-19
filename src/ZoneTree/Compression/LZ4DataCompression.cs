using K4os.Compression.LZ4;

namespace Tenray.ZoneTree.Compression;

public static class LZ4DataCompression
{
    public static Memory<byte> Compress(Memory<byte> bytes, int level)
    {
        return LZ4Pickler.Pickle(bytes.Span, (LZ4Level)level);
    }

    public static byte[] Decompress(Memory<byte> compressedBytes)
    {
        return LZ4Pickler.Unpickle(compressedBytes.Span);
    }

    public static byte[] DecompressFast(Memory<byte> compressedBytes, int decompressedLength)
    {
        var bytes = new byte[decompressedLength];
        LZ4Pickler.Unpickle(compressedBytes.Span, bytes);
        return bytes;
    }
}
