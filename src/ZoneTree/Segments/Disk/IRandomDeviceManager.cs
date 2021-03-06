namespace Tenray.ZoneTree.Segments.Disk;

public interface IRandomAccessDeviceManager
{
    IRandomAccessDevice GetReadOnlyDevice(
        int segmentId, string category, bool isCompressed, int compressionBlockSize);

    IRandomAccessDevice CreateWritableDevice(
        int segmentId, string category, bool isCompressed, int compressionBlockSize);

    bool DeviceExists(int segmentId, string category);

    int DeviceCount { get; }

    int ReadOnlyDeviceCount { get; }

    int WritableDeviceCount { get; }

    IReadOnlyList<IRandomAccessDevice> GetReadOnlyDevices();

    IReadOnlyList<IRandomAccessDevice> GetWritableDevices();

    IReadOnlyList<IRandomAccessDevice> GetDevices();

    void CloseAllDevices();

    void RemoveReadOnlyDevice(int segmentId, string category);

    void RemoveWritableDevice(int segmentId, string category);

    void DropStore();
}
