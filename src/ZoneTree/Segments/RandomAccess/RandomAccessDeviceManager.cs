using Tenray.ZoneTree.AbstractFileStream;
using Tenray.ZoneTree.Logger;
using Tenray.ZoneTree.Options;

namespace Tenray.ZoneTree.Segments.Disk;

public sealed class RandomAccessDeviceManager : IRandomAccessDeviceManager
{
    public ILogger Logger { get; }

    public IFileStreamProvider FileStreamProvider { get; }

    readonly Dictionary<string, IRandomAccessDevice> ReadOnlyDevices = new();

    readonly Dictionary<string, IRandomAccessDevice> WritableDevices = new();

    readonly string DataDirectory;

    public int DeviceCount => ReadOnlyDevices.Count + WritableDevices.Count;

    public int ReadOnlyDeviceCount => ReadOnlyDevices.Count;

    public int WritableDeviceCount => WritableDevices.Count;

    public RandomAccessDeviceManager(
        ILogger logger, 
        IFileStreamProvider fileStreamProvider,
        string dataDirectory = "data")
    {
        Logger = logger;
        FileStreamProvider = fileStreamProvider;
        DataDirectory = dataDirectory;
        FileStreamProvider.CreateDirectory(dataDirectory);
    }

    public void CloseAllDevices()
    {
        lock (this)
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
    }

    public IRandomAccessDevice CreateWritableDevice(
        long segmentId, string category, 
        bool isCompressed, int compressionBlockSize, int maxCachedBlockCount,
        bool deleteIfExists, bool backupIfDelete,
        CompressionMethod compressionMethod,
        int compressionLevel,
        long blockCacheReplacementWarningDuration)
    {
        lock (this)
        {
            var key = GetDeviceKey(segmentId, category);
            if (WritableDevices.ContainsKey(key))
            {
                throw new Exception($"Writable device can be created only once. segmentId: {segmentId:X} category: {category}");
            }
            var filePath = GetFilePath(segmentId, category);
            if (deleteIfExists) DeleteFileIfExists(backupIfDelete, filePath);
            if (isCompressed)
            {
                filePath += ".z";
            }
            if (deleteIfExists) DeleteFileIfExists(backupIfDelete, filePath);
            IRandomAccessDevice device = isCompressed ?
                new CompressedFileRandomAccessDevice(
                    Logger,
                    maxCachedBlockCount,
                    FileStreamProvider,
                    segmentId, category, this, filePath, true,
                    compressionBlockSize,
                    compressionMethod,
                    compressionLevel,
                    blockCacheReplacementWarningDuration) :
                new FileRandomAccessDevice(
                    FileStreamProvider,
                    segmentId, category, this, filePath, true);
            WritableDevices.Add(key, device);
            return device;
        }
    }

    void DeleteFileIfExists(bool backupIfDelete, string filePath)
    {
        if (!FileStreamProvider.FileExists(filePath))
            return;
        if (backupIfDelete)
            FileStreamProvider.Replace(filePath,
                filePath + ".backup." + Guid.NewGuid().ToString("N"), null);
        else
            FileStreamProvider.DeleteFile(filePath);
    }

    public IReadOnlyList<IRandomAccessDevice> GetDevices()
    {
        var a = GetReadOnlyDevices();
        var b = GetWritableDevices();
        return a.Concat(b).ToArray();
    }

    static string GetDeviceKey(long segmentId, string category)
    {
        return segmentId + category;
    }

    public string GetFilePath(long segmentId, string category)
    {
        return Path.Combine(DataDirectory, segmentId + category);
    }

    public IRandomAccessDevice GetReadOnlyDevice(
        long segmentId, string category, 
        bool isCompressed, int compressionBlockSize, 
        int maxCachedBlockCount, 
        CompressionMethod compressionMethod,
        int compressionLevel,
        long blockCacheReplacementWarningDuration)
    {
        lock (this)
        {
            var key = GetDeviceKey(segmentId, category);
            if (ReadOnlyDevices.TryGetValue(key, out var device))
                return device;

            if (WritableDevices.ContainsKey(key))
            {
                throw new Exception($"ReadOnly device can be created after writable device is closed. segmentId: {segmentId} category: {category}");
            }
            var filePath = GetFilePath(segmentId, category);
            if (isCompressed && FileStreamProvider.FileExists(filePath))
            {
                isCompressed = false;
            }
            var compressedfilePath = filePath + ".z";
            if (!isCompressed && FileStreamProvider.FileExists(compressedfilePath))
            {
                isCompressed = true;
            }

            if (isCompressed)
            {
                filePath = compressedfilePath;
            }

            device = isCompressed ?
                new CompressedFileRandomAccessDevice(
                    Logger,
                    maxCachedBlockCount,
                    FileStreamProvider,
                    segmentId, category, this, filePath, false,
                    compressionBlockSize,
                    compressionMethod,
                    compressionLevel,
                    blockCacheReplacementWarningDuration) :
                new FileRandomAccessDevice(
                    FileStreamProvider,
                    segmentId, category, this, filePath, false);
            ReadOnlyDevices.Add(key, device);
            return device;
        }
    }

    public IReadOnlyList<IRandomAccessDevice> GetReadOnlyDevices()
    {
        lock (this)
        {
            return ReadOnlyDevices.Values.ToArray();
        }
    }

    public IReadOnlyList<IRandomAccessDevice> GetWritableDevices()
    {
        lock (this)
        {
            return WritableDevices.Values.ToArray();
        }
    }

    public void RemoveReadOnlyDevice(long segmentId, string category)
    {
        lock (this)
        {
            var key = GetDeviceKey(segmentId, category);
            ReadOnlyDevices.Remove(key);
        }
    }

    public void RemoveWritableDevice(long segmentId, string category)
    {
        lock (this)
        {
            var key = GetDeviceKey(segmentId, category);
            WritableDevices.Remove(key);
        }
    }

    public bool DeviceExists(long segmentId, string category)
    {
        lock (this)
        {
            var filePath = GetFilePath(segmentId, category);
            return FileStreamProvider.FileExists(filePath);
        }
    }

    public void DropStore()
    {
        lock (this)
        {
            if (FileStreamProvider.DirectoryExists(DataDirectory))
                FileStreamProvider.DeleteDirectory(DataDirectory, true);
        }
    }
}