namespace ZoneTree.AbstractFileStream;

internal sealed class InMemoryFile
{
  public readonly Lock SyncRoot = new();

  byte[] Buffer = [];

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
      Buffer.AsSpan((int)position, count).CopyTo(destination);
      return count;
    }
  }

  public void Write(long position, ReadOnlySpan<byte> source)
  {
    lock (SyncRoot)
    {
      var end = checked(position + source.Length);
      EnsureCapacity(end);
      if (position > Length)
        Array.Clear(Buffer, (int)Length, (int)(position - Length));
      source.CopyTo(Buffer.AsSpan((int)position));
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
        Array.Clear(Buffer, (int)Length, (int)(value - Length));
      Length = value;
    }
  }

  public byte[] ToArray()
  {
    lock (SyncRoot)
    {
      var copy = new byte[(int)Length];
      Buffer.AsSpan(0, (int)Length).CopyTo(copy);
      return copy;
    }
  }

  void EnsureCapacity(long capacity)
  {
    if (capacity > Array.MaxLength)
      throw new IOException("In-memory file is too large.");

    if (Buffer.LongLength >= capacity)
      return;

    var newCapacity = Buffer.Length == 0 ? 256 : Buffer.Length;
    while (newCapacity < capacity)
    {
      var nextCapacity = newCapacity * 2L;
      newCapacity = (int)Math.Min(nextCapacity, Array.MaxLength);
      if (newCapacity == Array.MaxLength)
        break;
    }

    if (newCapacity < capacity)
      throw new IOException("In-memory file is too large.");

    Array.Resize(ref Buffer, newCapacity);
  }
}
