using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using ZoneTree.Options;
using ZoneTree.Segments.MultiPart;
using ZoneTree.Segments.DiskSegmentVariations;
using ZoneTree.Segments.RandomAccess;

namespace ZoneTree.Segments.Disk;

public static class DiskSegmentFactory
{
  public static IDiskSegment<TKey, TValue>
      CreateDiskSegment<TKey, TValue>(long segmentId, ZoneTreeOptions<TKey, TValue> options)
  {
    if (options.RandomAccessDeviceManager.DeviceExists(
        segmentId,
        DiskSegmentConstants.MultiPartDiskSegmentCategory,
        isCompressed: false))
    {
      return new MultiPartDiskSegment<TKey, TValue>(segmentId, options);
    }

    var hasFixedSizeKey = !RuntimeHelpers.IsReferenceOrContainsReferences<TKey>();
    var hasFixedSizeValue = !RuntimeHelpers.IsReferenceOrContainsReferences<TValue>();
    if (hasFixedSizeKey && hasFixedSizeValue)
      return new FixedSizeKeyAndValueDiskSegment<TKey, TValue>(segmentId, options);
    if (hasFixedSizeKey)
      return new FixedSizeKeyDiskSegment<TKey, TValue>(segmentId, options);
    if (hasFixedSizeValue)
      return new FixedSizeValueDiskSegment<TKey, TValue>(segmentId, options);
    return new VariableSizeDiskSegment<TKey, TValue>(segmentId, options);
  }

  public static IDiskSegment<TKey, TValue> CreateDiskSegment<TKey, TValue>(
      long segmentId,
      ZoneTreeOptions<TKey, TValue> options,
      IRandomAccessDevice dataHeaderDevice,
      IRandomAccessDevice dataDevice)
  {
    var hasFixedSizeKey = !RuntimeHelpers.IsReferenceOrContainsReferences<TKey>();
    var hasFixedSizeValue = !RuntimeHelpers.IsReferenceOrContainsReferences<TValue>();
    if (hasFixedSizeKey && hasFixedSizeValue)
      return new FixedSizeKeyAndValueDiskSegment<TKey, TValue>(segmentId, options, dataDevice);
    if (hasFixedSizeKey)
      return new FixedSizeKeyDiskSegment<TKey, TValue>(segmentId, options, dataHeaderDevice, dataDevice);
    if (hasFixedSizeValue)
      return new FixedSizeValueDiskSegment<TKey, TValue>(segmentId, options, dataHeaderDevice, dataDevice);
    return new VariableSizeDiskSegment<TKey, TValue>(segmentId, options, dataHeaderDevice, dataDevice);
  }

  public static IDiskSegment<TKey, TValue> CreateDiskSegment<TKey, TValue>(
      DiskSegmentFile[] files,
      ZoneTreeOptions<TKey, TValue> options)
  {
    if (files == null || files.Length == 0)
      throw new ArgumentException("Disk segment files cannot be empty.", nameof(files));

    var rootFile = files.FirstOrDefault(x =>
        x.FileName.EndsWith(
            DiskSegmentConstants.MultiPartDiskSegmentCategory,
            StringComparison.Ordinal)) ?? files[0];

    return CreateDiskSegment(rootFile.SegmentId, options, files);
  }

  public static IDiskSegment<TKey, TValue> CreateDiskSegment<TKey, TValue>(
      long segmentId,
      ZoneTreeOptions<TKey, TValue> options,
      DiskSegmentFile[] files)
  {
    if (files == null || files.Length == 0)
      throw new ArgumentException("Disk segment files cannot be empty.", nameof(files));

    if (!files.Any(x => x.SegmentId == segmentId))
      throw new ArgumentException(
          $"Disk segment file is missing for segment {segmentId}.",
          nameof(files));

    return CreateDiskSegment(segmentId, options);
  }
}
