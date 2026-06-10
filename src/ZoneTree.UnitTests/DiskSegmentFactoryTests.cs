using ZoneTree.Options;
using ZoneTree.Segments.Disk;
using ZoneTree.Serializers;

namespace ZoneTree.UnitTests;

public sealed class DiskSegmentFactoryTests
{
  [Test]
  public void CreatesFixedSizeKeyAndValueDiskSegmentFromFiles()
  {
    AssertDiskSegmentCanBeCreatedFromFiles<int, int>(
        "CreatesFixedSizeKeyAndValueDiskSegmentFromFiles",
        DiskSegmentMode.SingleDiskSegment,
        i => i,
        i => i + 1,
        factory => factory.DisableDeletion());
  }

  [Test]
  public void CreatesFixedSizeKeyDiskSegmentFromFiles()
  {
    AssertDiskSegmentCanBeCreatedFromFiles<int, string>(
        "CreatesFixedSizeKeyDiskSegmentFromFiles",
        DiskSegmentMode.SingleDiskSegment,
        i => i,
        i => "value-" + i,
        factory => factory
            .DisableDeletion()
            .SetValueSerializer(new UnicodeStringSerializer()));
  }

  [Test]
  public void CreatesFixedSizeValueDiskSegmentFromFiles()
  {
    AssertDiskSegmentCanBeCreatedFromFiles<string, int>(
        "CreatesFixedSizeValueDiskSegmentFromFiles",
        DiskSegmentMode.SingleDiskSegment,
        i => i.ToString("D5"),
        i => i + 1,
        factory => factory
            .DisableDeletion()
            .SetKeySerializer(new UnicodeStringSerializer()));
  }

  [Test]
  public void CreatesVariableSizeDiskSegmentFromFiles()
  {
    AssertDiskSegmentCanBeCreatedFromFiles<string, string>(
        "CreatesVariableSizeDiskSegmentFromFiles",
        DiskSegmentMode.SingleDiskSegment,
        i => i.ToString("D5"),
        i => "value-" + i,
        factory => factory
            .DisableDeletion()
            .SetKeySerializer(new UnicodeStringSerializer())
            .SetValueSerializer(new UnicodeStringSerializer()));
  }

  [Test]
  public void CreatesMultiPartDiskSegmentFromFiles()
  {
    AssertDiskSegmentCanBeCreatedFromFiles<int, int>(
        "CreatesMultiPartDiskSegmentFromFiles",
        DiskSegmentMode.MultiPartDiskSegment,
        i => i,
        i => i + 1,
        factory => factory.DisableDeletion(),
        configureDiskSegmentOptions: options =>
        {
          options.MinimumRecordCount = 3;
          options.MaximumRecordCount = 4;
        },
        recordCount: 12);
  }

  static void AssertDiskSegmentCanBeCreatedFromFiles<TKey, TValue>(
      string name,
      DiskSegmentMode diskSegmentMode,
      Func<int, TKey> keyFactory,
      Func<int, TValue> valueFactory,
      Func<ZoneTreeFactory<TKey, TValue>, ZoneTreeFactory<TKey, TValue>> configureFactory,
      Action<DiskSegmentOptions> configureDiskSegmentOptions = null,
      int recordCount = 32)
  {
    var dataPath = "data/" + name;
    DeleteDirectory(dataPath);

    DiskSegmentFile[] files;
    ZoneTreeOptions<TKey, TValue> options;
    var keys = new TKey[recordCount];
    var values = new TValue[recordCount];

    using (var zoneTree = configureFactory(new ZoneTreeFactory<TKey, TValue>())
        .SetDataDirectory(dataPath)
        .SetWriteAheadLogDirectory(dataPath)
        .ConfigureWriteAheadLogOptions(options =>
            options.WriteAheadLogMode = WriteAheadLogMode.None)
        .ConfigureDiskSegmentOptions(options =>
        {
          options.DiskSegmentMode = diskSegmentMode;
          configureDiskSegmentOptions?.Invoke(options);
        })
        .OpenOrCreate())
    {
      for (var i = 0; i < recordCount; ++i)
      {
        keys[i] = keyFactory(i);
        values[i] = valueFactory(i);
        zoneTree.Upsert(keys[i], values[i]);
      }

      zoneTree.Maintenance.MoveMutableSegmentForward();
      zoneTree.Maintenance.StartMergeOperation().Join();

      files = zoneTree.Maintenance.DiskSegment.GetFiles();
      options = zoneTree.Maintenance.CloneOptions();

      Assert.That(files, Is.Not.Empty);
    }

    using (var diskSegment = DiskSegmentFactory.CreateDiskSegment<TKey, TValue>(
        files,
        options))
    {
      for (var i = 0; i < recordCount; ++i)
      {
        Assert.That(diskSegment.TryGet(keys[i], out var value), Is.True);
        Assert.That(value, Is.EqualTo(values[i]));
      }
    }

    DeleteDirectory(dataPath);
  }

  static void DeleteDirectory(string path)
  {
    if (Directory.Exists(path))
      Directory.Delete(path, true);
  }
}
