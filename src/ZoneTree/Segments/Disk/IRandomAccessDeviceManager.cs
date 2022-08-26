using Tenray.ZoneTree.AbstractFileStream;
using Tenray.ZoneTree.Options;

namespace Tenray.ZoneTree.Segments.Disk;

public interface IRandomAccessDeviceManager
{
    IFileStreamProvider FileStreamProvider { get; }

    IRandomAccessDevice GetReadOnlyDevice(
        long segmentId, string category, bool isCompressed, 
        int compressionBlockSize, int maxCachedBlockCount,
        CompressionMethod compressionMethod,
        int compressionLevel,
        long blockCacheReplacementWarningDuration);

    IRandomAccessDevice CreateWritableDevice(
        long segmentId, string category, bool isCompressed, 
        int compressionBlockSize, int maxCachedBlockCount,
        bool deleteIfExists, bool backupIfDelete,
        CompressionMethod compressionMethod,
        int compressionLevel,
        long blockCacheReplacementWarningDuration);

    bool DeviceExists(long segmentId, string category);

    int DeviceCount { get; }

    int ReadOnlyDeviceCount { get; }

    int WritableDeviceCount { get; }

    IReadOnlyList<IRandomAccessDevice> GetReadOnlyDevices();

    IReadOnlyList<IRandomAccessDevice> GetWritableDevices();

    IReadOnlyList<IRandomAccessDevice> GetDevices();

    void CloseAllDevices();

    void RemoveReadOnlyDevice(long segmentId, string category);

    void RemoveWritableDevice(long segmentId, string category);

    void DropStore();

    string GetFilePath(long segmentId, string category);
}
