using System.Text;

namespace Tenray.ZoneTree.AbstractFileStream;

public sealed class InMemoryFileStreamProvider : IFileStreamProvider
{
    readonly Dictionary<string, byte[]> Files = new();
    readonly HashSet<string> Directories = new();

    public IFileStream CreateFileStream(
        string path,
        FileMode mode,
        FileAccess access,
        FileShare share,
        int bufferSize = 4096,
        FileOptions options = FileOptions.None)
    {
        lock (this)
        {
            if (!Files.ContainsKey(path))
            {
                if (mode == FileMode.Open)
                    throw new FileNotFoundException(path);
                Files[path] = Array.Empty<byte>();
            }
            else if (mode == FileMode.CreateNew)
            {
                throw new IOException($"File {path} already exists.");
            }
            else if (mode == FileMode.Create)
            {
                Files[path] = Array.Empty<byte>();
            }
            else if (mode == FileMode.Truncate)
            {
                Files[path] = Array.Empty<byte>();
            }
            var bytes = Files[path];
            var stream = new InMemoryFileStream(this, path, bytes);
            if (mode == FileMode.Append)
                stream.Seek(0, SeekOrigin.End);
            return stream;
        }
    }

    public bool FileExists(string path)
    {
        lock (this) return Files.ContainsKey(path);
    }

    public bool DirectoryExists(string path)
    {
        lock (this) return Directories.Contains(path);
    }

    public void CreateDirectory(string path)
    {
        lock (this) Directories.Add(path);
    }

    public void DeleteFile(string path)
    {
        lock (this) Files.Remove(path);
    }

    public void DeleteDirectory(string path, bool recursive)
    {
        lock (this)
        {
            Directories.Remove(path);
            if (recursive)
            {
                var toRemove = Files.Keys.Where(x => x.StartsWith(path)).ToList();
                foreach (var f in toRemove)
                    Files.Remove(f);
            }
        }
    }

    public string ReadAllText(string path)
    {
        lock (this) return Encoding.UTF8.GetString(Files[path]);
    }

    public byte[] ReadAllBytes(string path)
    {
        lock (this)
        {
            var b = Files[path];
            var copy = new byte[b.Length];
            Buffer.BlockCopy(b, 0, copy, 0, b.Length);
            return copy;
        }
    }

    public void Replace(
        string sourceFileName,
        string destinationFileName,
        string destinationBackupFileName)
    {
        lock (this)
        {
            if (destinationBackupFileName != null && Files.ContainsKey(destinationFileName))
            {
                Files[destinationBackupFileName] = Files[destinationFileName];
            }
            Files[destinationFileName] = Files.ContainsKey(sourceFileName) ? Files[sourceFileName] : Array.Empty<byte>();
            Files.Remove(sourceFileName);
        }
    }

    public DurableFileWriter GetDurableFileWriter()
    {
        return new DurableFileWriter(this);
    }

    public IReadOnlyList<string> GetDirectories(string path)
    {
        lock (this)
        {
            return Directories.Where(x => x.StartsWith(path)).ToArray();
        }
    }

    public string CombinePaths(string path1, string path2)
    {
        return Path.Combine(path1, path2);
    }

    internal void UpdateFile(string path, byte[] data)
    {
        lock (this)
        {
            Files[path] = data;
        }
    }
}
