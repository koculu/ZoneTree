namespace Tenray.ZoneTree.Segments.Disk;

public class RandomAccessDeviceManager : IRandomAccessDeviceManager
{
    readonly Dictionary<string, IRandomAccessDevice> ReadOnlyDevices = new();

    readonly Dictionary<string, IRandomAccessDevice> WritableDevices = new();

    readonly string DataDirectory;

    public int DeviceCount => ReadOnlyDevices.Count + WritableDevices.Count;

    public int ReadOnlyDeviceCount => ReadOnlyDevices.Count;

    public int WritableDeviceCount => WritableDevices.Count;

    public RandomAccessDeviceManager(string dataDirectory = "data")
    {
        DataDirectory = dataDirectory;
        Directory.CreateDirectory(dataDirectory);
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

    public IRandomAccessDevice CreateWritableDevice(int segmentId, string category)
    {
        var key = GetDeviceKey(segmentId, category);
        if (WritableDevices.ContainsKey(key))
        {
            throw new Exception($"Writable device can be created only once. guid: {segmentId:X} category: {category}");
        }
        var filePath = GetFilePath(segmentId, category);
        var device = new FileRandomAccessDevice(segmentId, category, this, filePath, true);
        WritableDevices.Add(key, device);
        return device;
    }

    public IReadOnlyList<IRandomAccessDevice> GetDevices()
    {
        var a = GetReadOnlyDevices();
        var b = GetWritableDevices();
        return a.Concat(b).ToArray();
    }

    private static string GetDeviceKey(int segmentId, string category)
    {
        return segmentId + category;
    }

    private string GetFilePath(int segmentId, string category)
    {
        return Path.Combine(DataDirectory, segmentId + category);
    }

    public IRandomAccessDevice GetReadOnlyDevice(int segmentId, string category)
    {
        var key = GetDeviceKey(segmentId, category);
        if (ReadOnlyDevices.TryGetValue(key, out var device))
            return device;

        if (WritableDevices.ContainsKey(key))
        {
            throw new Exception($"ReadOnly device can be created after writable device is closed. segmentId: {segmentId} category: {category}");
        }
        var filePath = GetFilePath(segmentId, category);
        device = new FileRandomAccessDevice(segmentId, category, this, filePath, false);
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

    public void RemoveReadOnlyDevice(int segmentId, string category)
    {
        var key = GetDeviceKey(segmentId, category);
        ReadOnlyDevices.Remove(key);
    }

    public void RemoveWritableDevice(int segmentId, string category)
    {
        var key = GetDeviceKey(segmentId, category);
        WritableDevices.Remove(key);
    }

    public bool DeviceExists(int segmentId, string category)
    {
        var filePath = GetFilePath(segmentId, category);
        return File.Exists(filePath);
    }

    public void DropStore()
    {
        if (Directory.Exists(DataDirectory))
            Directory.Delete(DataDirectory, true);
    }
}