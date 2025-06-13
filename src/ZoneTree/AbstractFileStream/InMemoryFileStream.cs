using System;
using System.IO;

namespace Tenray.ZoneTree.AbstractFileStream;

public sealed class InMemoryFileStream : MemoryStream, IFileStream
{
    readonly InMemoryFileStreamProvider Provider;

    public string FilePath { get; }

    public InMemoryFileStream(
        InMemoryFileStreamProvider provider,
        string path,
        byte[] buffer)
    {
        Provider = provider;
        FilePath = path;
        if (buffer.Length > 0)
            Write(buffer, 0, buffer.Length);
        Position = 0;
    }

    public void Flush(bool flushToDisk)
    {
        Flush();
    }

    public int ReadFaster(byte[] buffer, int offset, int count)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int read = Read(buffer, offset + totalRead, count - totalRead);
            if (read == 0)
                throw new EndOfStreamException();
            totalRead += read;
        }
        return totalRead;
    }

    public Stream ToStream() => this;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Provider.UpdateFile(FilePath, ToArray());
        }
        base.Dispose(disposing);
    }
}
