namespace ZoneTree.AbstractFileStream;

public sealed class InMemoryFileStream : Stream, IFileStream
{
  readonly InMemoryFile File;

  readonly FileAccess Access;

  readonly bool AppendMode;

  long _position;

  bool IsDisposed;

  public string FilePath { get; }

  internal InMemoryFileStream(
      string path,
      InMemoryFile file,
      FileAccess access,
      bool appendMode)
  {
    FilePath = path;
    File = file;
    Access = access;
    AppendMode = appendMode;
  }

  public override bool CanRead => !IsDisposed && (Access & FileAccess.Read) != 0;

  public override bool CanSeek => !IsDisposed;

  public override bool CanWrite => !IsDisposed && (Access & FileAccess.Write) != 0;

  public override long Length
  {
    get
    {
      ThrowIfDisposed();
      return File.GetLength();
    }
  }

  public override long Position
  {
    get
    {
      ThrowIfDisposed();
      return _position;
    }
    set
    {
      ThrowIfDisposed();
      ArgumentOutOfRangeException.ThrowIfNegative(value);
      _position = value;
    }
  }

  public override void Flush()
  {
    ThrowIfDisposed();
  }

  public void Flush(bool flushToDisk) => Flush();

  public override int Read(byte[] buffer, int offset, int count)
  {
    ValidateArrayArguments(buffer, offset, count);
    return Read(buffer.AsSpan(offset, count));
  }

  public override int Read(Span<byte> buffer)
  {
    ThrowIfDisposed();
    ThrowIfCannotRead();
    var read = File.Read(_position, buffer);
    _position += read;
    return read;
  }

  public int ReadFaster(byte[] buffer, int offset, int count)
  {
    ValidateArrayArguments(buffer, offset, count);
    var totalRead = 0;
    while (totalRead < count)
    {
      var read = Read(buffer, offset + totalRead, count - totalRead);
      if (read == 0)
        throw new EndOfStreamException();
      totalRead += read;
    }
    return totalRead;
  }

  public override ValueTask<int> ReadAsync(
      Memory<byte> buffer,
      CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();
    return ValueTask.FromResult(Read(buffer.Span));
  }

  public override Task<int> ReadAsync(
      byte[] buffer,
      int offset,
      int count,
      CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();
    return Task.FromResult(Read(buffer, offset, count));
  }

  public override int ReadByte()
  {
    Span<byte> buffer = stackalloc byte[1];
    return Read(buffer) == 0 ? -1 : buffer[0];
  }

  public override long Seek(long offset, SeekOrigin origin)
  {
    ThrowIfDisposed();
    var position = origin switch
    {
      SeekOrigin.Begin => offset,
      SeekOrigin.Current => _position + offset,
      SeekOrigin.End => Length + offset,
      _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, null)
    };
    if (position < 0)
      throw new IOException("An attempt was made to move the position before the beginning of the stream.");
    _position = position;
    return _position;
  }

  public override void SetLength(long value)
  {
    ThrowIfDisposed();
    ThrowIfCannotWrite();
    File.SetLength(value);
    if (_position > value)
      _position = value;
  }

  public override void Write(byte[] buffer, int offset, int count)
  {
    ValidateArrayArguments(buffer, offset, count);
    Write(buffer.AsSpan(offset, count));
  }

  public override void Write(ReadOnlySpan<byte> buffer)
  {
    ThrowIfDisposed();
    ThrowIfCannotWrite();
    if (AppendMode)
      _position = Length;
    File.Write(_position, buffer);
    _position += buffer.Length;
  }

  public override Task WriteAsync(
      byte[] buffer,
      int offset,
      int count,
      CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();
    Write(buffer, offset, count);
    return Task.CompletedTask;
  }

  public override ValueTask WriteAsync(
      ReadOnlyMemory<byte> buffer,
      CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();
    Write(buffer.Span);
    return ValueTask.CompletedTask;
  }

  public override void WriteByte(byte value)
  {
    Span<byte> buffer = stackalloc byte[1];
    buffer[0] = value;
    Write(buffer);
  }

  public Stream ToStream() => this;

  protected override void Dispose(bool disposing)
  {
    IsDisposed = true;
    base.Dispose(disposing);
  }

  static void ValidateArrayArguments(byte[] buffer, int offset, int count)
  {
    ArgumentNullException.ThrowIfNull(buffer);
    ArgumentOutOfRangeException.ThrowIfNegative(offset);
    ArgumentOutOfRangeException.ThrowIfNegative(count);
    if (buffer.Length - offset < count)
      throw new ArgumentException("Offset and count exceed the buffer length.");
  }

  void ThrowIfCannotRead()
  {
    if (!CanRead)
      throw new NotSupportedException("Stream does not support reading.");
  }

  void ThrowIfCannotWrite()
  {
    if (!CanWrite)
      throw new NotSupportedException("Stream does not support writing.");
  }

  void ThrowIfDisposed()
  {
    ObjectDisposedException.ThrowIf(IsDisposed, this);
  }
}
