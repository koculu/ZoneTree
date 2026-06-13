namespace ZoneTree.AbstractFileStream;

internal sealed class InMemoryFile
{
  internal const int SmallChunkSize = 4 * 1024;

  internal const int LargeChunkSize = 8 * 1024 * 1024;

  internal const int LargeChunkThreshold = 1024 * 1024;

  public readonly Lock SyncRoot = new();

  readonly List<byte[]> Chunks = [];

  int ChunkSize = SmallChunkSize;

  long Length;

  public long GetLength()
  {
    lock (SyncRoot)
    {
      return Length;
    }
  }

  public int Read(long position, Span<byte> destination)
  {
    lock (SyncRoot)
    {
      if (position >= Length)
        return 0;
      var count = (int)Math.Min(destination.Length, Length - position);
      var remaining = count;
      var destinationOffset = 0;
      while (remaining > 0)
      {
        var chunkIndex = GetChunkIndex(position);
        var chunkOffset = GetChunkOffset(position);
        var copyLength = Math.Min(remaining, ChunkSize - chunkOffset);
        Chunks[chunkIndex]
            .AsSpan(chunkOffset, copyLength)
            .CopyTo(destination.Slice(destinationOffset, copyLength));
        position += copyLength;
        destinationOffset += copyLength;
        remaining -= copyLength;
      }
      return count;
    }
  }

  public void Write(long position, ReadOnlySpan<byte> source)
  {
    if (source.Length == 0)
      return;

    lock (SyncRoot)
    {
      var end = checked(position + source.Length);
      EnsureCapacity(end);
      if (position > Length)
        ClearRange(Length, position - Length);
      var remaining = source.Length;
      var sourceOffset = 0;
      while (remaining > 0)
      {
        var chunkIndex = GetChunkIndex(position);
        var chunkOffset = GetChunkOffset(position);
        var copyLength = Math.Min(remaining, ChunkSize - chunkOffset);
        source
            .Slice(sourceOffset, copyLength)
            .CopyTo(Chunks[chunkIndex].AsSpan(chunkOffset, copyLength));
        position += copyLength;
        sourceOffset += copyLength;
        remaining -= copyLength;
      }
      if (end > Length)
        Length = end;
    }
  }

  public void SetLength(long value)
  {
    ArgumentOutOfRangeException.ThrowIfNegative(value);
    lock (SyncRoot)
    {
      EnsureCapacity(value);
      if (value > Length)
        ClearRange(Length, value - Length);
      Length = value;
      TrimExtraChunks();
      if (Length == 0 && ChunkSize != SmallChunkSize)
        ResetToSmallChunks();
    }
  }

  public byte[] ToArray()
  {
    lock (SyncRoot)
    {
      if (Length > Array.MaxLength)
        throw new IOException("In-memory file is too large to read into a single byte array.");
      var copy = new byte[(int)Length];
      var written = 0;
      var remaining = copy.Length;
      for (var i = 0; remaining > 0; ++i)
      {
        var copyLength = Math.Min(remaining, ChunkSize);
        Chunks[i].AsSpan(0, copyLength).CopyTo(copy.AsSpan(written, copyLength));
        written += copyLength;
        remaining -= copyLength;
      }
      return copy;
    }
  }

  void EnsureCapacity(long capacity)
  {
    if (capacity == 0)
      return;

    if (ChunkSize == SmallChunkSize && capacity > LargeChunkThreshold)
      PromoteToLargeChunks();

    var requiredChunks = GetRequiredChunkCount(capacity);
    while (Chunks.Count < requiredChunks)
    {
      Chunks.Add(new byte[ChunkSize]);
    }
  }

  void ClearRange(long position, long length)
  {
    while (length > 0)
    {
      var chunkIndex = GetChunkIndex(position);
      var chunkOffset = GetChunkOffset(position);
      var clearLength = (int)Math.Min(length, ChunkSize - chunkOffset);
      Array.Clear(Chunks[chunkIndex], chunkOffset, clearLength);
      position += clearLength;
      length -= clearLength;
    }
  }

  void TrimExtraChunks()
  {
    var requiredChunks = GetRequiredChunkCount(Length);
    if (Chunks.Count > requiredChunks)
      Chunks.RemoveRange(requiredChunks, Chunks.Count - requiredChunks);
  }

  void PromoteToLargeChunks()
  {
    var oldChunks = Chunks.ToArray();
    var oldChunkSize = ChunkSize;
    Chunks.Clear();
    ChunkSize = LargeChunkSize;
    EnsureCapacity(Length);

    var position = 0L;
    foreach (var oldChunk in oldChunks)
    {
      var remaining = (int)Math.Min(oldChunkSize, Length - position);
      if (remaining <= 0)
        break;
      var oldChunkOffset = 0;
      while (remaining > 0)
      {
        var newChunkIndex = GetChunkIndex(position);
        var newChunkOffset = GetChunkOffset(position);
        var copyLength = Math.Min(remaining, ChunkSize - newChunkOffset);
        oldChunk
            .AsSpan(oldChunkOffset, copyLength)
            .CopyTo(Chunks[newChunkIndex].AsSpan(newChunkOffset, copyLength));
        oldChunkOffset += copyLength;
        position += copyLength;
        remaining -= copyLength;
      }
    }
  }

  void ResetToSmallChunks()
  {
    Chunks.Clear();
    ChunkSize = SmallChunkSize;
  }

  int GetRequiredChunkCount(long capacity)
  {
    if (capacity == 0)
      return 0;
    var requiredChunks = checked((capacity + ChunkSize - 1) / ChunkSize);
    if (requiredChunks > int.MaxValue)
      throw new IOException("In-memory file is too large.");
    return (int)requiredChunks;
  }

  int GetChunkIndex(long position) => (int)(position / ChunkSize);

  int GetChunkOffset(long position) => (int)(position % ChunkSize);
}
