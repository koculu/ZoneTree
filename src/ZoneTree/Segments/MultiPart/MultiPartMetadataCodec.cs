using System.Buffers.Binary;
using ZoneTree.Compression;
using ZoneTree.Options;

namespace ZoneTree.Segments.MultiPart;

internal static class MultiPartMetadataCodec
{
  internal const CompressionMethod CurrentCompressionMethod = CompressionMethod.Zstd;

  internal const int CurrentCompressionLevel = CompressionLevels.Zstd0;

  const int MagicSize = 16;

  const byte Version = 1;

  const int PrefixSize =
      MagicSize + sizeof(byte) + sizeof(byte) + sizeof(int) + sizeof(int);

  static ReadOnlySpan<byte> Magic => "ZoneTreeMPH-v001"u8;

  public static Memory<byte> Encode(Memory<byte> bytes)
  {
    var payload = DataCompression.Compress(
        CurrentCompressionMethod,
        CurrentCompressionLevel,
        bytes);

    var result = new byte[PrefixSize + payload.Length];
    var span = result.AsSpan();
    Magic.CopyTo(span);

    var offset = MagicSize;
    span[offset++] = Version;
    span[offset++] = (byte)CurrentCompressionMethod;
    BinaryPrimitives.WriteInt32LittleEndian(
        span.Slice(offset, sizeof(int)),
        CurrentCompressionLevel);
    offset += sizeof(int);
    BinaryPrimitives.WriteInt32LittleEndian(
        span.Slice(offset, sizeof(int)),
        payload.Length);
    offset += sizeof(int);
    payload.Span.CopyTo(span[offset..]);
    return result;
  }

  public static Memory<byte> Decode(Memory<byte> bytes)
  {
    if (!HasEnvelope(bytes))
      return DataCompression.Decompress(CompressionMethod.LZ4, bytes);

    var (method, payload) = ReadEnvelope(bytes);
    return DataCompression.Decompress(method, payload);
  }

  static bool HasEnvelope(Memory<byte> bytes)
  {
    if (bytes.Length < MagicSize)
      return false;

    return bytes.Span[..MagicSize].SequenceEqual(Magic);
  }

  static (CompressionMethod method, Memory<byte> payload) ReadEnvelope(
      Memory<byte> bytes)
  {
    if (bytes.Length < PrefixSize)
      throw new InvalidDataException("Multipart metadata envelope is incomplete.");

    var span = bytes.Span;
    var offset = MagicSize;
    var version = span[offset++];
    if (version != Version)
      throw new InvalidDataException(
          $"Unsupported multipart metadata envelope version: {version}.");

    var method = (CompressionMethod)span[offset++];
    var level = BinaryPrimitives.ReadInt32LittleEndian(
        span.Slice(offset, sizeof(int)));
    offset += sizeof(int);
    if (!CompressionLevels.IsValid(method, level))
      throw new InvalidDataException(
          $"Invalid multipart metadata compression profile: {method}, level {level}.");

    var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(
        span.Slice(offset, sizeof(int)));
    offset += sizeof(int);
    if (payloadLength < 0 || payloadLength != span.Length - offset)
      throw new InvalidDataException(
          "Multipart metadata envelope payload length is invalid.");

    return (method, bytes.Slice(offset, payloadLength));
  }
}
