using System.Text;

namespace ZoneTree.AbstractFileStream;

public sealed class InMemoryFileStreamProvider : IFileStreamProvider
{
  readonly Dictionary<string, byte[]> Files = [];

  readonly HashSet<string> Directories = [];


  readonly Lock SyncRoot = new();

  public IFileStream CreateFileStream(
      string path,
      FileMode mode,
      FileAccess access,
      FileShare share,
      int bufferSize = 4096,
      FileOptions options = FileOptions.None)
  {
    lock (SyncRoot)
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
    lock (SyncRoot) return Files.ContainsKey(path);
  }

  public bool DirectoryExists(string path)
  {
    lock (SyncRoot) return Directories.Contains(path);
  }

  public void CreateDirectory(string path)
  {
    lock (SyncRoot) Directories.Add(path);
  }

  public void DeleteFile(string path)
  {
    lock (SyncRoot) Files.Remove(path);
  }

  public void DeleteDirectory(string path, bool recursive)
  {
    lock (SyncRoot)
    {
      Directories.Remove(path);
      if (recursive)
      {
        var toRemove = Files.Keys.Where(x => x.StartsWith(path, StringComparison.Ordinal)).ToList();
        foreach (var f in toRemove)
          Files.Remove(f);
      }
    }
  }

  public string ReadAllText(string path)
  {
    lock (SyncRoot) return Encoding.UTF8.GetString(Files[path]);
  }

  public byte[] ReadAllBytes(string path)
  {
    lock (SyncRoot)
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
    lock (SyncRoot)
    {
      if (destinationBackupFileName != null &&
          Files.TryGetValue(destinationFileName, out var destinationBytes))
      {
        Files[destinationBackupFileName] = destinationBytes;
      }

      Files[destinationFileName] = Files.TryGetValue(sourceFileName, out var sourceBytes)
        ? sourceBytes
        : [];

      Files.Remove(sourceFileName);
    }
  }

  public DurableFileWriter GetDurableFileWriter()
  {
    return new DurableFileWriter(this);
  }

  public IReadOnlyList<string> GetDirectories(string path)
  {
    lock (SyncRoot)
    {
      return Directories.Where(x => x.StartsWith(path, StringComparison.Ordinal)).ToArray();
    }
  }

  public string CombinePaths(string path1, string path2)
  {
    return Path.Combine(path1, path2);
  }

  internal void UpdateFile(string path, byte[] data)
  {
    lock (SyncRoot)
    {
      Files[path] = data;
    }
  }
}
