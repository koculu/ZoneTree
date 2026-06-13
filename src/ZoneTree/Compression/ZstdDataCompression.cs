using ZstdSharp;

namespace ZoneTree.Compression;

public static class ZstdDataCompression
{
  public static Memory<byte> Compress(Memory<byte> bytes, int level)
  {
    using var compressor = new Compressor(level);
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
    decompressor.Unwrap(compressedBytes.Span, decompressed.AsSpan());
    return decompressed;
  }
}
