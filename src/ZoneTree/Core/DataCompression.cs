using System.IO.Compression;
using Tenray.ZoneTree.Segments.Disk;

namespace Tenray.ZoneTree.Core;

public static class DataCompression 
{
    public static byte[] Compress(byte[] bytes)
    {
        using var msOutput = new MemoryStream();
        using var gzs = new GZipStream(msOutput, CompressionLevel.Fastest, false);
        gzs.Write(bytes);
        gzs.Flush();
        return msOutput.ToArray();
    }

    public static byte[] Decompress(byte[] compressedBytes)
    {
        using var msInput = new MemoryStream(compressedBytes);
        using var msOutput = new MemoryStream();
        using var gzs = new GZipStream(msInput, CompressionMode.Decompress);
        gzs.CopyTo(msOutput);
        var decompressed = msOutput.ToArray();
        return decompressed;
    }
}
