using Tenray.ZoneTree.AbstractFileStream;

namespace Tenray.ZoneTree.Segments.Disk;

public class RandomAccessDeviceManager : IRandomAccessDeviceManager
{
    readonly IFileStreamProvider FileStreamProvider;

    readonly Dictionary<string, IRandomAccessDevice> ReadOnlyDevices = new();

    readonly Dictionary<string, IRandomAccessDevice> WritableDevices = new();

    readonly string DataDirectory;

    public int DeviceCount => ReadOnlyDevices.Count + WritableDevices.Count;

    public int ReadOnlyDeviceCount => ReadOnlyDevices.Count;

    public int WritableDeviceCount => WritableDevices.Count;

    public RandomAccessDeviceManager(IFileStreamProvider fileStreamProvider, string dataDirectory = "data")
    {
        FileStreamProvider = fileStreamProvider;
        DataDirectory = dataDirectory;
        FileStreamProvider.CreateDirectory(dataDirectory);
    }

    public void CloseAllDevices()
    {
        foreach (var device in ReadOnlyDevices.Values)
        {
            device.Close();
        }
        foreach (var device in WritableDevices.Values)
        {
            device.Close();
        }
    }

    public IRandomAccessDevice CreateWritableDevice(
        long segmentId, string category, 
        bool isCompressed, int compressionBlockSize, int maxCachedBlockCount,
        bool deleteIfExists, bool backupIfDelete)
    {
        var key = GetDeviceKey(segmentId, category);
        if (WritableDevices.ContainsKey(key))
        {
            throw new Exception($"Writable device can be created only once. guid: {segmentId:X} category: {category}");
        }
        var filePath = GetFilePath(segmentId, category);
        if (deleteIfExists && FileStreamProvider.FileExists(filePath))
        {
            if (backupIfDelete)
                FileStreamProvider.Replace(filePath, 
                    filePath + ".backup." + Guid.NewGuid().ToString("N"), null);
            else
                FileStreamProvider.DeleteFile(filePath);
        }
        IRandomAccessDevice device = isCompressed ?
            new CompressedFileRandomAccessDevice(
                maxCachedBlockCount,
                FileStreamProvider,
                segmentId, category, this, filePath, true, compressionBlockSize) :
            new FileRandomAccessDevice(
                FileStreamProvider,
                segmentId, category, this, filePath, true);
        WritableDevices.Add(key, device);
        return device;
    }

    public IReadOnlyList<IRandomAccessDevice> GetDevices()
    {
        var a = GetReadOnlyDevices();
        var b = GetWritableDevices();
        return a.Concat(b).ToArray();
    }

    private static string GetDeviceKey(long segmentId, string category)
    {
        return segmentId + category;
    }

    private string GetFilePath(long segmentId, string category)
    {
        return Path.Combine(DataDirectory, segmentId + category);
    }

    public IRandomAccessDevice GetReadOnlyDevice(
        long segmentId, string category, 
        bool isCompressed, int compressionBlockSize, 
        int maxCachedBlockCount)
    {
        var key = GetDeviceKey(segmentId, category);
        if (ReadOnlyDevices.TryGetValue(key, out var device))
            return device;

        if (WritableDevices.ContainsKey(key))
        {
            throw new Exception($"ReadOnly device can be created after writable device is closed. segmentId: {segmentId} category: {category}");
        }
        var filePath = GetFilePath(segmentId, category);
        device = isCompressed ?
            new CompressedFileRandomAccessDevice(
                maxCachedBlockCount,
                FileStreamProvider,
                segmentId, category, this, filePath, false, compressionBlockSize) :
            new FileRandomAccessDevice(
                FileStreamProvider,
                segmentId, category, this, filePath, false);
        ReadOnlyDevices.Add(key, device);
        return device;
    }

    public IReadOnlyList<IRandomAccessDevice> GetReadOnlyDevices()
    {
        return ReadOnlyDevices.Values.ToArray();
    }

    public IReadOnlyList<IRandomAccessDevice> GetWritableDevices()
    {
        return WritableDevices.Values.ToArray();
    }

    public void RemoveReadOnlyDevice(long segmentId, string category)
    {
        var key = GetDeviceKey(segmentId, category);
        ReadOnlyDevices.Remove(key);
    }

    public void RemoveWritableDevice(long segmentId, string category)
    {
        var key = GetDeviceKey(segmentId, category);
        WritableDevices.Remove(key);
    }

    public bool DeviceExists(long segmentId, string category)
    {
        var filePath = GetFilePath(segmentId, category);
        return FileStreamProvider.FileExists(filePath);
    }

    public void DropStore()
    {
        if (FileStreamProvider.DirectoryExists(DataDirectory))
            FileStreamProvider.DeleteDirectory(DataDirectory, true);
    }
}