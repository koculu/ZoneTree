using Tenray.ZoneTree.AbstractFileStream;
using Tenray.ZoneTree.Exceptions;

namespace Tenray.ZoneTree.WAL;

public static class IncrementalLogAppender
{
    public static void AppendLogToTheBackupFile(
        IFileStreamProvider fileStreamProvider, 
        string backupFile, 
        Func<byte[]> getBytesDelegate)
    {
        var backupDataOffset = sizeof(long) * 3;
        using var fs = fileStreamProvider.CreateFileStream(
                backupFile,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                4096);
        var br = new BinaryReader(fs.ToStream());
        bool hasLengthStamp = fs.Length > backupDataOffset;
        if (hasLengthStamp)
        {
            // read the length-stamp three times and select the most common one,
            // to ensure the length was not written partially to the file.
            var lengthInTheFile1 = br.ReadInt64();
            var lengthInTheFile2 = br.ReadInt64();
            var lengthInTheFile3 = br.ReadInt64();
            if (lengthInTheFile1 != lengthInTheFile2)
            {
                if (lengthInTheFile1 == lengthInTheFile3)
                    lengthInTheFile1 = lengthInTheFile3;
                else if (lengthInTheFile2 == lengthInTheFile3)
                    lengthInTheFile1 = lengthInTheFile2;
                else
                {
                    // 3 length-stamps are different from each other.
                    // We might copy the corrupted file to a backup location
                    // and start a new backup file immediately
                    // to not to interrupt the system on full log corruption.
                    // For now, we prefer throwing an exception.
                    throw new WriteAheadLogFullLogCorruptionException(backupFile);
                }
            }

            // make sure the file has no crashed backup data.
            if (fs.Length > lengthInTheFile1)
            {
                fs.SetLength(lengthInTheFile1);
            }
        }
        else
        {
            fs.SetLength(0);
            fs.Write(BitConverter.GetBytes(fs.Length));
            fs.Write(BitConverter.GetBytes(fs.Length));
            fs.Write(BitConverter.GetBytes(fs.Length));
            fs.Flush(true);
        }

        // first append the additional data.
        var bytes = getBytesDelegate();

        fs.Seek(0, SeekOrigin.End);
        fs.Write(bytes);
        fs.Flush(true);

        // now write the file length-stamps.
        // what happens if a crash happens with partial write of the fs Length?
        // To prevent that, we write and flush the length-stamp three times with separate flushes..
        fs.Position = 0;
        fs.Write(BitConverter.GetBytes(fs.Length));
        fs.Flush(true);
        fs.Write(BitConverter.GetBytes(fs.Length));
        fs.Flush(true);
        fs.Write(BitConverter.GetBytes(fs.Length));
        fs.Flush(true);
    }
}
