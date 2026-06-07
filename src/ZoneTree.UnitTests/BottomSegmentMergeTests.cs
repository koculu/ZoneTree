using System.Text.Json;
using ZoneTree.AbstractFileStream;
using ZoneTree.Comparers;
using ZoneTree.Core;
using ZoneTree.Logger;
using ZoneTree.Options;
using ZoneTree.Segments;
using ZoneTree.Segments.Disk;
using ZoneTree.Segments.InMemory;
using ZoneTree.Segments.MultiPart;
using ZoneTree.Segments.NullDisk;
using ZoneTree.Segments.RandomAccess;
using ZoneTree.Serializers;
using ZoneTree.WAL;

namespace ZoneTree.UnitTests;

public sealed class BottomSegmentMergeTests
{
  [Test]
  public void IntIntBottomMerge()
  {
    var dataPath = "data/IntIntBottomMerge";
    if (Directory.Exists(dataPath))
      Directory.Delete(dataPath, true);

    var zoneTree = new ZoneTreeFactory<int, int>()
        .DisableDeletion()
        .SetDiskSegmentMaxItemCount(10)
        .SetDataDirectory(dataPath)
        .ConfigureDiskSegmentOptions(
            x => x.DiskSegmentMode = DiskSegmentMode.SingleDiskSegment)
        .OpenOrCreate();
    var m = zoneTree.Maintenance;
    var insertCount = 100;
    var p = 10;
    for (var i = 0; i < insertCount; ++i)
    {
      zoneTree.Upsert(i, i + i);
      if (i % p == p - 1)
      {
        m.MoveMutableSegmentForward();
        m.StartMergeOperation().Join();
        for (var j = 0; j < p - 10; ++j)
        {
          ++i;
          if (i >= insertCount)
            break;
          zoneTree.Upsert(i, i + i);
        }
        ++p;
      }
    }

    var expected1 = new long[] { 21, 19, 17, 15, 13, 11 };
    var expected2 = new long[] { 21, 51, 13, 11 };

    var sum =
        m.BottomSegments.Sum(x => x.Length) +
        m.InMemoryRecordCount +
        m.DiskSegment.Length;
    var lens = m.BottomSegments.Select(x => x.Length).ToArray();
    Assert.That(sum, Is.EqualTo(insertCount));
    Assert.That(lens, Is.EqualTo(expected1));
    m.StartBottomSegmentsMergeOperation(1, 3).Join();

    lens = m.BottomSegments.Select(x => x.Length).ToArray();
    Assert.That(lens, Is.EqualTo(expected2));
    zoneTree.Dispose();

    zoneTree = new ZoneTreeFactory<int, int>()
        .DisableDeletion()
        .SetDiskSegmentMaxItemCount(10)
        .SetDataDirectory(dataPath)
        .OpenOrCreate();

    lens = m.BottomSegments.Select(x => x.Length).ToArray();
    Assert.That(lens, Is.EqualTo(expected2));
    zoneTree.Maintenance.Drop();
    zoneTree.Dispose();
  }

  [Test]
  public void PartialMultiPartBottomMergeCarriesPartsFromMergedBottomSegment()
  {
    var options = CreateMultiPartBottomMergeOptions();
    var idProvider = new IncrementalIdProvider();

    var untouchedBottomSegment = CreateMultiPartSegment(
        options,
        idProvider,
        CreateRange(200, 10));
    var firstMergedBottomSegment = CreateMultiPartSegment(
        options,
        idProvider,
        CreateRange(0, 10),
        CreateRange(100, 10));
    var lastMergedBottomSegment = CreateMultiPartSegment(
        options,
        idProvider,
        CreateRange(50, 10),
        CreateRange(150, 10));
    var mutableSegment = new MutableSegment<int, int>(
        options,
        idProvider.NextId(),
        new IncrementalIdProvider());
    var zoneTree = new ZoneTree<int, int>(
        options,
        new ZoneTreeMeta(),
        Array.Empty<IReadOnlySegment<int, int>>(),
        mutableSegment,
        new NullDiskSegment<int, int>(),
        [
            untouchedBottomSegment,
                firstMergedBottomSegment,
                lastMergedBottomSegment
        ],
        idProvider.LastId);

    try
    {
      zoneTree.Maintenance.StartBottomSegmentsMergeOperation(1, 2).Join();

      foreach (var key in ExpectedKeysAfterPartialMultiPartMerge())
      {
        Assert.That(
            zoneTree.TryGet(key, out var value),
            Is.True,
            $"Key {key} was lost after partial multipart bottom merge.");
        Assert.That(value, Is.EqualTo(key));
      }
    }
    finally
    {
      zoneTree.Dispose();
      options.RandomAccessDeviceManager.DropStore();
    }
  }

  static ZoneTreeOptions<int, int> CreateMultiPartBottomMergeOptions()
  {
    var logger = new ConsoleLogger(LogLevel.Error);
    var options = new ZoneTreeOptions<int, int>
    {
      Comparer = new Int32ComparerAscending(),
      KeySerializer = new Int32Serializer(),
      ValueSerializer = new Int32Serializer(),
      Logger = logger,
      WriteAheadLogProvider = new NullWriteAheadLogProvider(),
      RandomAccessDeviceManager = new RandomAccessDeviceManager(
            logger,
            new InMemoryFileStreamProvider(),
            "data/PartialMultiPartBottomMerge"),
      DiskSegmentOptions = new DiskSegmentOptions
      {
        DiskSegmentMode = DiskSegmentMode.MultiPartDiskSegment,
        CompressionMethod = CompressionMethod.None,
        CompressionLevel = 0,
        CompressionBlockSize = 1024,
        MinimumRecordCount = 1,
        MaximumRecordCount = 100,
        DefaultSparseArrayStepSize = 0,
      },
    };
    options.DisableDeletion();
    options.Validate();
    return options;
  }

  static IDiskSegment<int, int> CreateMultiPartSegment(
      ZoneTreeOptions<int, int> options,
      IIncrementalIdProvider idProvider,
      params int[][] parts)
  {
    using IDiskSegmentCreator<int, int> creator =
        new MultiPartDiskSegmentCreator<int, int>(options, idProvider);
    foreach (var keys in parts)
    {
      var part = CreatePartSegment(options, idProvider, keys);
      creator.Append(
          part,
          keys[0],
          keys[^1],
          keys[0],
          keys[^1]);
    }
    return creator.CreateReadOnlyDiskSegment();
  }

  static IDiskSegment<int, int> CreatePartSegment(
      ZoneTreeOptions<int, int> options,
      IIncrementalIdProvider idProvider,
      IReadOnlyList<int> keys)
  {
    using var creator = new DiskSegmentCreator<int, int>(options, idProvider);
    foreach (var key in keys)
    {
      creator.Append(key, key, IteratorPosition.None);
    }
    return creator.CreateReadOnlyDiskSegment();
  }

  static int[] CreateRange(int start, int count) =>
      Enumerable.Range(start, count).ToArray();

  static IEnumerable<int> ExpectedKeysAfterPartialMultiPartMerge() =>
      Enumerable.Range(0, 10)
          .Concat(Enumerable.Range(50, 10))
          .Concat(Enumerable.Range(100, 10))
          .Concat(Enumerable.Range(150, 10))
          .Concat(Enumerable.Range(200, 10));
}
