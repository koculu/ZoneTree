using System.Text.Json;
using ZoneTree.Core;
using ZoneTree.Options;
using ZoneTree.WAL;

namespace ZoneTree.UnitTests;

public sealed class ZoneTreeMetaOptionsTests
{
  [Test]
  public void CreatePersistsActiveOptionsInMetadata()
  {
    var dataPath = CreateDataPath();
    var expected = CreateExpectedOptions("created", CompressionMethod.Brotli);

    try
    {
      using var zoneTree = OpenConfiguredZoneTree(dataPath, expected);
      zoneTree.Maintenance.SaveMetaData();

      AssertMetadataOptions(dataPath, expected);
    }
    finally
    {
      DeleteDataPath(dataPath);
    }
  }

  [Test]
  public void OpenExistingTreePersistsActiveOptionsOverLoadedMetadata()
  {
    var dataPath = CreateDataPath();
    var staleOptions = CreateExpectedOptions("stale", CompressionMethod.LZ4);
    var activeOptions = CreateExpectedOptions("active", CompressionMethod.Zstd);

    try
    {
      using (var zoneTree = OpenConfiguredZoneTree(dataPath, staleOptions))
      {
        zoneTree.Maintenance.SaveMetaData();
      }

      AssertMetadataOptions(dataPath, staleOptions);

      using (OpenConfiguredZoneTree(dataPath, activeOptions))
      {
        AssertMetadataOptions(dataPath, activeOptions);
      }
    }
    finally
    {
      DeleteDataPath(dataPath);
    }
  }

  static IZoneTree<string, string> OpenConfiguredZoneTree(
      string dataPath,
      ExpectedOptions expected)
  {
    return new ZoneTreeFactory<string, string>()
        .SetDataDirectory(dataPath)
        .SetMutableSegmentMaxItemCount(expected.MutableSegmentMaxItemCount)
        .SetDiskSegmentMaxItemCount(expected.DiskSegmentMaxItemCount)
        .ConfigureWriteAheadLogOptions(options =>
        {
          options.WriteAheadLogMode = expected.WriteAheadLogMode;
          options.CustomOptions = expected.WriteAheadLogCustomOptions;
          options.CompressionBlockSize = expected.WriteAheadLogCompressionBlockSize;
          options.CompressionMethod = expected.WriteAheadLogCompressionMethod;
          options.CompressionLevel = expected.WriteAheadLogCompressionLevel;
          options.EnableIncrementalBackup = expected.EnableIncrementalBackup;
          options.AsyncCompressedModeOptions.EmptyQueuePollInterval =
              expected.AsyncCompressedModeEmptyQueuePollInterval;
          options.SyncCompressedModeOptions.EnableTailWriterJob =
              expected.SyncCompressedModeEnableTailWriterJob;
          options.SyncCompressedModeOptions.TailWriterJobInterval =
              expected.SyncCompressedModeTailWriterJobInterval;
        })
        .ConfigureDiskSegmentOptions(options =>
        {
          options.DiskSegmentMode = expected.DiskSegmentMode;
          options.CompressionBlockSize = expected.DiskSegmentCompressionBlockSize;
          options.CompressionMethod = expected.DiskSegmentCompressionMethod;
          options.CompressionLevel = expected.DiskSegmentCompressionLevel;
          options.MaximumRecordCount = expected.DiskSegmentMaximumRecordCount;
          options.MinimumRecordCount = expected.DiskSegmentMinimumRecordCount;
          options.KeyCacheSize = expected.DiskSegmentKeyCacheSize;
          options.ValueCacheSize = expected.DiskSegmentValueCacheSize;
          options.KeyCacheRecordLifeTimeInMillisecond =
              expected.DiskSegmentKeyCacheRecordLifeTimeInMillisecond;
          options.ValueCacheRecordLifeTimeInMillisecond =
              expected.DiskSegmentValueCacheRecordLifeTimeInMillisecond;
          options.DefaultSparseArrayStepSize = expected.DefaultSparseArrayStepSize;
        })
        .OpenOrCreate();
  }

  static ExpectedOptions CreateExpectedOptions(
      string customOptions,
      CompressionMethod compressionMethod)
  {
    return compressionMethod switch
    {
      CompressionMethod.LZ4 => new ExpectedOptions(
          MutableSegmentMaxItemCount: 37,
          DiskSegmentMaxItemCount: 73,
          WriteAheadLogMode: WriteAheadLogMode.Sync,
          WriteAheadLogCustomOptions: customOptions,
          WriteAheadLogCompressionBlockSize: 128 * 1024,
          WriteAheadLogCompressionMethod: CompressionMethod.LZ4,
          WriteAheadLogCompressionLevel: CompressionLevels.LZ4HighCompression3,
          EnableIncrementalBackup: true,
          AsyncCompressedModeEmptyQueuePollInterval: 17,
          SyncCompressedModeEnableTailWriterJob: false,
          SyncCompressedModeTailWriterJobInterval: 19,
          DiskSegmentMode: DiskSegmentMode.SingleDiskSegment,
          DiskSegmentCompressionBlockSize: 256 * 1024,
          DiskSegmentCompressionMethod: CompressionMethod.LZ4,
          DiskSegmentCompressionLevel: CompressionLevels.LZ4HighCompression4,
          DiskSegmentMaximumRecordCount: 101,
          DiskSegmentMinimumRecordCount: 51,
          DiskSegmentKeyCacheSize: 11,
          DiskSegmentValueCacheSize: 13,
          DiskSegmentKeyCacheRecordLifeTimeInMillisecond: 23,
          DiskSegmentValueCacheRecordLifeTimeInMillisecond: 29,
          DefaultSparseArrayStepSize: 31),

      CompressionMethod.Zstd => new ExpectedOptions(
          MutableSegmentMaxItemCount: 41,
          DiskSegmentMaxItemCount: 83,
          WriteAheadLogMode: WriteAheadLogMode.SyncCompressed,
          WriteAheadLogCustomOptions: customOptions,
          WriteAheadLogCompressionBlockSize: 192 * 1024,
          WriteAheadLogCompressionMethod: CompressionMethod.Zstd,
          WriteAheadLogCompressionLevel: CompressionLevels.Zstd1,
          EnableIncrementalBackup: false,
          AsyncCompressedModeEmptyQueuePollInterval: 43,
          SyncCompressedModeEnableTailWriterJob: true,
          SyncCompressedModeTailWriterJobInterval: 47,
          DiskSegmentMode: DiskSegmentMode.MultiPartDiskSegment,
          DiskSegmentCompressionBlockSize: 384 * 1024,
          DiskSegmentCompressionMethod: CompressionMethod.Zstd,
          DiskSegmentCompressionLevel: CompressionLevels.Zstd2,
          DiskSegmentMaximumRecordCount: 107,
          DiskSegmentMinimumRecordCount: 53,
          DiskSegmentKeyCacheSize: 17,
          DiskSegmentValueCacheSize: 19,
          DiskSegmentKeyCacheRecordLifeTimeInMillisecond: 31,
          DiskSegmentValueCacheRecordLifeTimeInMillisecond: 37,
          DefaultSparseArrayStepSize: 41),

      CompressionMethod.Brotli => new ExpectedOptions(
          MutableSegmentMaxItemCount: 43,
          DiskSegmentMaxItemCount: 89,
          WriteAheadLogMode: WriteAheadLogMode.SyncCompressed,
          WriteAheadLogCustomOptions: customOptions,
          WriteAheadLogCompressionBlockSize: 224 * 1024,
          WriteAheadLogCompressionMethod: CompressionMethod.Brotli,
          WriteAheadLogCompressionLevel: CompressionLevels.BrotliFastest,
          EnableIncrementalBackup: true,
          AsyncCompressedModeEmptyQueuePollInterval: 59,
          SyncCompressedModeEnableTailWriterJob: false,
          SyncCompressedModeTailWriterJobInterval: 61,
          DiskSegmentMode: DiskSegmentMode.MultiPartDiskSegment,
          DiskSegmentCompressionBlockSize: 512 * 1024,
          DiskSegmentCompressionMethod: CompressionMethod.Brotli,
          DiskSegmentCompressionLevel: CompressionLevels.BrotliSmallestSize,
          DiskSegmentMaximumRecordCount: 109,
          DiskSegmentMinimumRecordCount: 59,
          DiskSegmentKeyCacheSize: 23,
          DiskSegmentValueCacheSize: 29,
          DiskSegmentKeyCacheRecordLifeTimeInMillisecond: 41,
          DiskSegmentValueCacheRecordLifeTimeInMillisecond: 43,
          DefaultSparseArrayStepSize: 47),

      _ => throw new ArgumentOutOfRangeException(nameof(compressionMethod))
    };
  }

  static void AssertMetadataOptions(
      string dataPath,
      ExpectedOptions expected)
  {
    var metaFilePath = Path.Combine(dataPath, "0.json");
    var json = File.ReadAllText(metaFilePath);
    var meta = JsonSerializer.Deserialize<ZoneTreeMeta>(json);

    Assert.That(meta, Is.Not.Null);
    Assert.That(meta.MutableSegmentMaxItemCount, Is.EqualTo(expected.MutableSegmentMaxItemCount));
    Assert.That(meta.DiskSegmentMaxItemCount, Is.EqualTo(expected.DiskSegmentMaxItemCount));

    var walOptions = meta.WriteAheadLogOptions;
    Assert.That(walOptions, Is.Not.Null);
    Assert.That(walOptions.WriteAheadLogMode, Is.EqualTo(expected.WriteAheadLogMode));
    Assert.That(walOptions.CompressionBlockSize, Is.EqualTo(expected.WriteAheadLogCompressionBlockSize));
    Assert.That(walOptions.CompressionMethod, Is.EqualTo(expected.WriteAheadLogCompressionMethod));
    Assert.That(walOptions.CompressionLevel, Is.EqualTo(expected.WriteAheadLogCompressionLevel));
    Assert.That(walOptions.EnableIncrementalBackup, Is.EqualTo(expected.EnableIncrementalBackup));
    Assert.That(walOptions.AsyncCompressedModeOptions.EmptyQueuePollInterval,
        Is.EqualTo(expected.AsyncCompressedModeEmptyQueuePollInterval));
    Assert.That(walOptions.SyncCompressedModeOptions.EnableTailWriterJob,
        Is.EqualTo(expected.SyncCompressedModeEnableTailWriterJob));
    Assert.That(walOptions.SyncCompressedModeOptions.TailWriterJobInterval,
        Is.EqualTo(expected.SyncCompressedModeTailWriterJobInterval));

    var diskOptions = meta.DiskSegmentOptions;
    Assert.That(diskOptions, Is.Not.Null);
    Assert.That(diskOptions.DiskSegmentMode, Is.EqualTo(expected.DiskSegmentMode));
    Assert.That(diskOptions.CompressionBlockSize, Is.EqualTo(expected.DiskSegmentCompressionBlockSize));
    Assert.That(diskOptions.CompressionMethod, Is.EqualTo(expected.DiskSegmentCompressionMethod));
    Assert.That(diskOptions.CompressionLevel, Is.EqualTo(expected.DiskSegmentCompressionLevel));
    Assert.That(diskOptions.MaximumRecordCount, Is.EqualTo(expected.DiskSegmentMaximumRecordCount));
    Assert.That(diskOptions.MinimumRecordCount, Is.EqualTo(expected.DiskSegmentMinimumRecordCount));
    Assert.That(diskOptions.KeyCacheSize, Is.EqualTo(expected.DiskSegmentKeyCacheSize));
    Assert.That(diskOptions.ValueCacheSize, Is.EqualTo(expected.DiskSegmentValueCacheSize));
    Assert.That(diskOptions.KeyCacheRecordLifeTimeInMillisecond,
        Is.EqualTo(expected.DiskSegmentKeyCacheRecordLifeTimeInMillisecond));
    Assert.That(diskOptions.ValueCacheRecordLifeTimeInMillisecond,
        Is.EqualTo(expected.DiskSegmentValueCacheRecordLifeTimeInMillisecond));
    Assert.That(diskOptions.DefaultSparseArrayStepSize,
        Is.EqualTo(expected.DefaultSparseArrayStepSize));

    using var document = JsonDocument.Parse(json);
    var persistedCustomOptions = document.RootElement
        .GetProperty(nameof(ZoneTreeMeta.WriteAheadLogOptions))
        .GetProperty(nameof(WriteAheadLogOptions.CustomOptions))
        .GetString();
    Assert.That(persistedCustomOptions, Is.EqualTo(expected.WriteAheadLogCustomOptions));
  }

  static string CreateDataPath()
  {
    return Path.Combine(
        "data",
        $"{nameof(ZoneTreeMetaOptionsTests)}_{Guid.NewGuid():N}");
  }

  static void DeleteDataPath(string dataPath)
  {
    if (Directory.Exists(dataPath))
      Directory.Delete(dataPath, true);
  }

  sealed record ExpectedOptions(
      int MutableSegmentMaxItemCount,
      int DiskSegmentMaxItemCount,
      WriteAheadLogMode WriteAheadLogMode,
      string WriteAheadLogCustomOptions,
      int WriteAheadLogCompressionBlockSize,
      CompressionMethod WriteAheadLogCompressionMethod,
      int WriteAheadLogCompressionLevel,
      bool EnableIncrementalBackup,
      int AsyncCompressedModeEmptyQueuePollInterval,
      bool SyncCompressedModeEnableTailWriterJob,
      int SyncCompressedModeTailWriterJobInterval,
      DiskSegmentMode DiskSegmentMode,
      int DiskSegmentCompressionBlockSize,
      CompressionMethod DiskSegmentCompressionMethod,
      int DiskSegmentCompressionLevel,
      int DiskSegmentMaximumRecordCount,
      int DiskSegmentMinimumRecordCount,
      int DiskSegmentKeyCacheSize,
      int DiskSegmentValueCacheSize,
      int DiskSegmentKeyCacheRecordLifeTimeInMillisecond,
      int DiskSegmentValueCacheRecordLifeTimeInMillisecond,
      int DefaultSparseArrayStepSize);
}
