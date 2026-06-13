using System.IO.Compression;
using ZoneTree.AbstractFileStream;

namespace ZoneTree.Compression;

public static class GZipDataCompression
{
  public static Memory<byte> Compress(Memory<byte> bytes, int level)
  {
    using var msOutput = new MemoryStream(bytes.Length);
    using (var gzs = new GZipStream(msOutput, (CompressionLevel)level, true))
    {
      gzs.Write(bytes.Span);
    }
    return msOutput.ToArray();
  }

  public static byte[] Decompress(Memory<byte> compressedBytes)
  {
    using var pin = compressedBytes.Pin();
    using var msInput = compressedBytes.ToReadOnlyStream(pin);
    using var msOutput = new MemoryStream(compressedBytes.Length);
    using var gzs = new GZipStream(msInput, CompressionMode.Decompress);
    gzs.CopyTo(msOutput);
    var decompressed = msOutput.ToArray();
    return decompressed;
  }

  public static byte[] DecompressFast(Memory<byte> compressedBytes, int decompressedLength)
  {
    var decompressed = new byte[decompressedLength];
    using var pin = compressedBytes.Pin();
    using var msInput = compressedBytes.ToReadOnlyStream(pin);
    using var msOutput = new MemoryStream(decompressed, true);
    using var gzs = new GZipStream(msInput, CompressionMode.Decompress);
    gzs.CopyTo(msOutput);
    return decompressed;
  }
}
