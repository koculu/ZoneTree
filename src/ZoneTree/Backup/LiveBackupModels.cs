using ZoneTree.Options;

namespace ZoneTree.Backup;

public sealed class LiveBackupGeneration
{
  public long GenerationId { get; set; }

  public long LastOpIndex { get; set; }

  public long[] SegmentIds { get; set; } = [];

  public LiveBackupFile[] Files { get; set; } = [];

  public LiveBackupRecordBatch RecordBatch { get; set; }
}

public sealed class LiveBackupFile
{
  public long SegmentId { get; set; }

  public int Order { get; set; }

  public string FileName { get; set; }

  public long RecordCount { get; set; }

  public long ByteLength { get; set; }
}

public sealed class LiveBackupRecord
{
  public long RecordIndex { get; set; }

  public bool IsDeleted { get; set; }

  public ReadOnlyMemory<byte> Key { get; set; }

  public ReadOnlyMemory<byte> Value { get; set; }
}

public sealed class LiveBackupRecordBatch
{
  public long BatchId { get; set; }

  public long RecordCount { get; set; }

  public CompressionMethod CompressionMethod { get; set; } = CompressionMethod.None;

  public int CompressionLevel { get; set; }

  public int CompressionBlockSize { get; set; }

  public long UncompressedLength { get; set; }

  public long StoredLength { get; set; }
}
