using Tenray.ZoneTree.AbstractFileStream;
using Tenray.ZoneTree.Options;

namespace Tenray.ZoneTree.Segments.RandomAccess;

public interface IRandomAccessDeviceManager
{
    IFileStreamProvider FileStreamProvider { get; }

    IRandomAccessDevice GetReadOnlyDevice(
        long segmentId, string category, bool isCompressed,
        int compressionBlockSize,
        CompressionMethod compressionMethod,
        int compressionLevel);

    IRandomAccessDevice CreateWritableDevice(
        long segmentId, string category, bool isCompressed,
        int compressionBlockSize,
        bool deleteIfExists, bool backupIfDelete,
        CompressionMethod compressionMethod,
        int compressionLevel);

    bool DeviceExists(long segmentId, string category, bool isCompressed);

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
