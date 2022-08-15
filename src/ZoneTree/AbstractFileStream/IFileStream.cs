namespace Tenray.ZoneTree.AbstractFileStream;

public interface IFileStream : IDisposable
{
    string FilePath { get; }

    long Position { get; set; }

    long Length { get; }

    bool CanWrite { get; }

    bool CanTimeout { get; }

    bool CanSeek { get; }

    bool CanRead { get; }

    int ReadTimeout { get; set; }
    
    int WriteTimeout { get; set; }
    
    IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state);
   
    IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state);
    
    void Close();
   
    void CopyTo(Stream destination, int bufferSize);
    
    void CopyTo(Stream destination);
    
    Task CopyToAsync(Stream destination);
    
    Task CopyToAsync(Stream destination, int bufferSize);
    
    Task CopyToAsync(Stream destination, CancellationToken cancellationToken);
           
    Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken);
           
    ValueTask DisposeAsync();
   
    int EndRead(IAsyncResult asyncResult);
   
    void EndWrite(IAsyncResult asyncResult);
    
    void Flush();
    
    void Flush(bool flushToDisk);

    Task FlushAsync();
    
    Task FlushAsync(CancellationToken cancellationToken);
    
    int Read(Span<byte> buffer);
    
    int Read(byte[] buffer, int offset, int count);
    
    ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default);
    
    Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken);
    
    Task<int> ReadAsync(byte[] buffer, int offset, int count);
    
    int ReadByte();
    
    long Seek(long offset, SeekOrigin origin);
    
    void SetLength(long value);

    void Write(byte[] buffer, int offset, int count);
    
    void Write(ReadOnlySpan<byte> buffer);
    
    Task WriteAsync(byte[] buffer, int offset, int count);
    
    Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken);
   
    ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default);
    
    void WriteByte(byte value);

    Stream ToStream();
}
