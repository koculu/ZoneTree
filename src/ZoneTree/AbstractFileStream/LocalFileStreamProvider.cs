namespace Tenray.ZoneTree.AbstractFileStream;

public sealed class LocalFileStreamProvider : IFileStreamProvider
{
    public IFileStream CreateFileStream(
        string path,
        FileMode mode, 
        FileAccess access, 
        FileShare share, 
        int bufferSize = 4096,
        FileOptions options = FileOptions.None)
    {
        return new LocalFileStream(path, mode, access, share, bufferSize, options);
    }

    public void CreateDirectory(string path)
    {
        Directory.CreateDirectory(path);
    }

    public bool DirectoryExists(string path)
    {
        return Directory.Exists(path);
    }

    public void DeleteDirectory(string path, bool recursive)
    {
        Directory.Delete(path, recursive);
    }

    public bool FileExists(string path)
    {
        return File.Exists(path);
    }

    public void DeleteFile(string path)
    {
        File.Delete(path);
    }

    public string ReadAllText(string path)
    {
        return File.ReadAllText(path);
    }

    public byte[] ReadAllBytes(string path)
    {
        return File.ReadAllBytes(path);
    }

    public void Replace(
        string sourceFileName, 
        string destinationFileName,
        string destinationBackupFileName)
    {
        // File Replace is a fast operation in local filesystem. 
        // It uses file rename operation and it is atomic.
        File.Replace(sourceFileName, destinationFileName, destinationBackupFileName);
    }

    public DurableFileWriter GetDurableFileWriter()
    {
        return new DurableFileWriter(this);
    }
}
