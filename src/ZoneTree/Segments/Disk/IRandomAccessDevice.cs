namespace Tenray.ZoneTree.Segments.Disk;

public interface IRandomAccessDevice : IDisposable
{
    int SegmentId { get; }

    bool Writable { get; }

    long Length { get; }

    void ClearContent();

    long AppendBytes(byte[] bytes);

    byte[] GetBytes(long offset, int length);

    int GetBytes(long offset, byte[] buffer);

    void Close();

    void Delete();

    void Flush();
}
