using ZoneTree.Compression;
using ZoneTree.Options;

namespace ZoneTree.Backup;

/// <summary>
/// Reads the binary record-batch format used by live backup.
/// </summary>
public sealed class LiveBackupRecordBatchReader : IDisposable
{
  readonly Stream Source;

  readonly LiveBackupRecordBatch Batch;

  MemoryStream BlockBuffer;

  public LiveBackupRecordBatchReader(
      Stream source,
      LiveBackupRecordBatch batch)
  {
    Source = source ?? throw new ArgumentNullException(nameof(source));
    Batch = batch ?? throw new ArgumentNullException(nameof(batch));
  }

  public bool TryRead(out LiveBackupRecord record)
  {
    if (Batch.CompressionMethod == CompressionMethod.None)
      return TryReadRecord(Source, out record);

    while (BlockBuffer == null || BlockBuffer.Position == BlockBuffer.Length)
    {
      BlockBuffer?.Dispose();
      BlockBuffer = null;
      if (!TryReadCompressedBlock(out var block))
      {
        record = null;
        return false;
      }
      BlockBuffer = new MemoryStream(block, writable: false);
    }
    return TryReadRecord(BlockBuffer, out record);
  }

  bool TryReadCompressedBlock(out byte[] block)
  {
    block = null;
    if (!TryReadInt32(Source, out var uncompressedLength))
      return false;
    if (!TryReadInt32(Source, out var compressedLength))
      throw new EndOfStreamException();
    var compressed = ReadBytes(Source, compressedLength);
    var decompressed = DataCompression.Decompress(
        Batch.CompressionMethod,
        compressed,
        uncompressedLength);
    if (decompressed.Length != uncompressedLength)
      throw new InvalidDataException("Invalid live backup compressed block length.");
    block = decompressed.ToArray();
    return true;
  }

  static bool TryReadRecord(
      Stream source,
      out LiveBackupRecord record)
  {
    record = null;
    if (!TryReadInt64(source, out var recordIndex))
      return false;
    var isDeleted = source.ReadByte();
    if (isDeleted < 0)
      throw new EndOfStreamException();
    if (!TryReadInt32(source, out var keyLength))
      throw new EndOfStreamException();
    if (!TryReadInt32(source, out var valueLength))
      throw new EndOfStreamException();
    record = new LiveBackupRecord
    {
      RecordIndex = recordIndex,
      IsDeleted = isDeleted != 0,
      Key = ReadBytes(source, keyLength),
      Value = ReadBytes(source, valueLength)
    };
    return true;
  }

  static bool TryReadInt32(Stream source, out int value)
  {
    Span<byte> bytes = stackalloc byte[sizeof(int)];
    if (!TryReadExactly(source, bytes))
    {
      value = 0;
      return false;
    }
    value = BitConverter.ToInt32(bytes);
    return true;
  }

  static bool TryReadInt64(Stream source, out long value)
  {
    Span<byte> bytes = stackalloc byte[sizeof(long)];
    if (!TryReadExactly(source, bytes))
    {
      value = 0;
      return false;
    }
    value = BitConverter.ToInt64(bytes);
    return true;
  }

  static bool TryReadExactly(
      Stream source,
      Span<byte> bytes)
  {
    var read = source.Read(bytes);
    if (read == 0)
      return false;
    while (read < bytes.Length)
    {
      var count = source.Read(bytes[read..]);
      if (count == 0)
        throw new EndOfStreamException();
      read += count;
    }
    return true;
  }

  static byte[] ReadBytes(
      Stream source,
      int length)
  {
    if (length < 0)
      throw new InvalidDataException("Invalid live backup record length.");
    var bytes = new byte[length];
    var read = 0;
    while (read < length)
    {
      var count = source.Read(bytes, read, length - read);
      if (count == 0)
        throw new EndOfStreamException();
      read += count;
    }
    return bytes;
  }

  public void Dispose()
  {
    BlockBuffer?.Dispose();
    Source.Dispose();
  }
}
