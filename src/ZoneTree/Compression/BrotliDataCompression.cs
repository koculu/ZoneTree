using System.IO.Compression;

namespace ZoneTree.Compression;

public static class BrotliDataCompression
{
  const int DefaultWindow = 22;

  public static Memory<byte> Compress(Memory<byte> bytes, int level)
  {
    var compressed = new byte[BrotliEncoder.GetMaxCompressedLength(bytes.Length)];
    if (!BrotliEncoder.TryCompress(
        bytes.Span,
        compressed.AsSpan(),
        out var compressedLength,
        GetQuality(level),
        DefaultWindow))
    {
      throw new InvalidDataException("Brotli compression failed.");
    }
    return compressed.AsMemory(0, compressedLength);
  }

  public static byte[] Decompress(Memory<byte> compressedBytes, int decompressedLength)
  {
    var decompressed = new byte[decompressedLength];
    if (!BrotliDecoder.TryDecompress(
        compressedBytes.Span,
        decompressed.AsSpan(),
        out var bytesWritten) ||
        bytesWritten != decompressedLength)
    {
      throw new InvalidDataException("Brotli decompression failed.");
    }
    return decompressed;
  }

  static int GetQuality(int level)
  {
    return (CompressionLevel)level switch
    {
      CompressionLevel.NoCompression => 0,
      CompressionLevel.SmallestSize => 11,
      // Brotli Optimal is extremely slow.
      // treat Optimal like Fastest.
      _ => 1,
    };
  }
}
