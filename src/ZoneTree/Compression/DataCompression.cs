using ZoneTree.Options;

namespace ZoneTree.Compression;

public static class DataCompression
{
  public static Memory<byte> Compress(CompressionMethod method, int level, Memory<byte> bytes)
  {
    return method switch
    {
      CompressionMethod.LZ4 => LZ4DataCompression.Compress(bytes, level),
      CompressionMethod.Zstd => ZstdDataCompression.Compress(bytes, level),
      CompressionMethod.Brotli => BrotliDataCompression.Compress(bytes, level),
      CompressionMethod.Gzip => GZipDataCompression.Compress(bytes, level),
      CompressionMethod.None => bytes,
      _ => throw new ArgumentOutOfRangeException(nameof(method)),
    };
  }

  public static Memory<byte> Decompress(
      CompressionMethod method, Memory<byte> compressedBytes, int decompressedLength)
  {
    return method switch
    {
      CompressionMethod.LZ4 => LZ4DataCompression.Decompress(compressedBytes, decompressedLength),
      CompressionMethod.Zstd => ZstdDataCompression.Decompress(compressedBytes, decompressedLength),
      CompressionMethod.Brotli => BrotliDataCompression.Decompress(compressedBytes, decompressedLength),
      CompressionMethod.Gzip => GZipDataCompression.Decompress(compressedBytes, decompressedLength),
      CompressionMethod.None => compressedBytes.ToArray(),
      _ => throw new ArgumentOutOfRangeException(nameof(method)),
    };
  }
}
