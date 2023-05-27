using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Tenray.ZoneTree.Options;
using Tenray.ZoneTree.Segments.DiskSegmentVariations;
using Tenray.ZoneTree.Segments.RandomAccess;

namespace Tenray.ZoneTree.Segments.Disk;

public static class DiskSegmentFactory
{
    public static IDiskSegment<TKey, TValue>
        CreateDiskSegment<TKey, TValue>(long segmentId, ZoneTreeOptions<TKey, TValue> options)
    {
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
            return new FixedSizeKeyAndValueDiskSegment<TKey, TValue>(segmentId, options, dataHeaderDevice, dataDevice);
        if (hasFixedSizeKey)
            return new FixedSizeKeyDiskSegment<TKey, TValue>(segmentId, options, dataHeaderDevice, dataDevice);
        if (hasFixedSizeValue)
            return new FixedSizeValueDiskSegment<TKey, TValue>(segmentId, options, dataHeaderDevice, dataDevice);
        return new VariableSizeDiskSegment<TKey, TValue>(segmentId, options, dataHeaderDevice, dataDevice);
    }
}
