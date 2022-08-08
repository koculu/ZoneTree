﻿namespace Tenray.ZoneTree.Segments.Disk;

public interface IRandomAccessDeviceManager
{
    IRandomAccessDevice GetReadOnlyDevice(
        long segmentId, string category, bool isCompressed, 
        int compressionBlockSize, int maxCachedBlockCount);

    IRandomAccessDevice CreateWritableDevice(
        long segmentId, string category, bool isCompressed, 
        int compressionBlockSize, int maxCachedBlockCount,
        bool deleteIfExists, bool backupIfDelete);

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
}
