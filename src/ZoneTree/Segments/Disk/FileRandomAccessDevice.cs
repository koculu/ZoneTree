namespace Tenray.ZoneTree.Segments.Disk;

public sealed class FileRandomAccessDevice : IRandomAccessDevice
{
    readonly string Category;

    FileStream FileStream;

    readonly IRandomAccessDeviceManager RandomDeviceManager;

    public string FilePath { get; }

    public int SegmentId { get; }

    public bool Writable { get; }

    public long Length => FileStream.Length;

    public FileRandomAccessDevice(
        int segmentId,
        string category,
        IRandomAccessDeviceManager randomDeviceManager,
        string filePath, bool writable, int fileIOBufferSize = 4096)
    {
        SegmentId = segmentId;
        Category = category;
        RandomDeviceManager = randomDeviceManager;
        FilePath = filePath;
        Writable = writable;
        var fileMode = writable ? FileMode.OpenOrCreate : FileMode.Open;
        var fileAccess = writable ? FileAccess.ReadWrite : FileAccess.Read;
        var fileShare = writable ? FileShare.None : FileShare.Read;
        FileStream = new FileStream(filePath,
            fileMode,
            fileAccess,
            fileShare, fileIOBufferSize, false);
        if (writable)
        {
            FileStream.Seek(0, SeekOrigin.End);
        }
    }

    public long AppendBytes(byte[] bytes)
    {
        var pos = FileStream.Position;
        FileStream.Write(bytes);
        return pos;
    }

    public byte[] GetBytes(long offset, int length)
    {
        lock (this)
        {
            var bytes = new byte[length];
            FileStream.Seek(offset, SeekOrigin.Begin);
            FileStream.Read(bytes);
            return bytes;
        }
    }

    public int GetBytes(long offset, byte[] buffer)
    {
        lock (this)
        {
            FileStream.Seek(offset, SeekOrigin.Begin);
            return FileStream.Read(buffer);
        }
    }

    public void Dispose()
    {
        if (FileStream == null)
            return;
        Close();
    }

    public void Close()
    {
        if (FileStream == null)
            return;
        FileStream.Flush();
        FileStream.Dispose();
        FileStream = null;
        if (Writable)
            RandomDeviceManager.RemoveWritableDevice(SegmentId, Category);
        else
            RandomDeviceManager.RemoveReadOnlyDevice(SegmentId, Category);
    }

    public void Delete()
    {
        Dispose();
        File.Delete(FilePath);
    }

    public void Flush()
    {
        FileStream.Flush();
    }

    public void ClearContent()
    {
        FileStream.SetLength(0);
        FileStream.Seek(0, SeekOrigin.Begin);
    }
}
