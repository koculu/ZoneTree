namespace Tenray.ZoneTree.Segments.RandomAccess;

public interface IRandomAccessDevice : IDisposable
{
    long SegmentId { get; }

    bool Writable { get; }

    long Length { get; }

    /// <summary>
    /// Returns read buffer count.
    /// </summary>
    int ReadBufferCount { get; }

    void ClearContent();

    /// <summary>
    /// Appends bytes and returns the position of the appended bytes.
    /// </summary>
    /// <param name="bytes">Bytes</param>
    /// <returns>Position of the bytes.</returns>
    long AppendBytesReturnPosition(Memory<byte> bytes);

    Memory<byte> GetBytes(long offset, int length);

    int GetBytes(long offset, Memory<byte> buffer);

    void Close();

    void Delete();

    /// <summary>
    /// Seals the device.
    /// Sealed devices can not accept new writes
    /// and becomes immutable.
    /// </summary>
    void SealDevice();

    /// <summary>
    /// Releases inactive cached read buffers.
    /// The buffers that have not been accessed since given ticks are released.
    /// </summary>
    /// <returns>Total released read buffer count.</returns>
    int ReleaseInactiveCachedBuffers(long ticks);
}
