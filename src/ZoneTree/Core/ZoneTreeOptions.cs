using Tenray.ZoneTree.Exceptions;
using Tenray.ZoneTree.Collections;
using Tenray.ZoneTree.Segments.Disk;
using Tenray.ZoneTree.WAL;

namespace Tenray.ZoneTree.Core;

public delegate bool IsValueDeletedDelegate<TValue>(in TValue value);

public delegate void MarkValueDeletedDelegate<TValue>(ref TValue value);

public class ZoneTreeOptions<TKey, TValue>
{
    public int MutableSegmentMaxItemCount { get; set; } = 1_000_000;

    public IRefComparer<TKey> Comparer { get; set; }

    public ISerializer<TKey> KeySerializer { get; set; }

    public ISerializer<TValue> ValueSerializer { get; set; }

    public IsValueDeletedDelegate<TValue> IsValueDeleted = (in TValue x) => false;

    public MarkValueDeletedDelegate<TValue> MarkValueDeleted = (ref TValue x) => { x = default; };

    public bool EnableDiskSegmentCompression { get; set; } = true;

    /// <summary>
    /// Disk Segment compression block size.
    /// Default: 1 MB
    /// </summary>
    public int DiskSegmentCompressionBlockSize { get; set; } = 1024 * 1024;

    /// <summary>
    /// Disk segment mode.
    /// </summary>
    public DiskSegmentMode DiskSegmentMode { get; set; }
        = DiskSegmentMode.MultipleDiskSegments;

    /// <summary>
    /// Disk segment block cache limit.
    /// A disk segment cannot have more cache blocks than the limit.
    /// Total memory space that block cache can take is
    /// = DiskSegmentCompressionBlockSize X DiskSegmentBlockCacheLimit
    /// Default: 1024 * 1024 * 32 = 32 MB
    /// </summary>
    public int DiskSegmentBlockCacheLimit { get; set; } = 32;

    /// <summary>
    /// If MultipleDiskSegments mode is enabled, it is the upper bound 
    /// record count of a disk segment.
    /// A disk segment cannot have record count more than this value.
    /// </summary>
    public int DiskSegmentMaximumRecordCount { get; set; } = 3_000_000;

    /// <summary>
    /// If MultipleDiskSegments mode is enabled,
    /// the minimum record count cannot be lower than this value
    /// unless there isn't enough records.
    /// </summary>
    public int DiskSegmentMinimumRecordCount { get; set; } = 1_500_000;

    public bool TryValidate(out Exception exception)
    {
        if (KeySerializer == null)
        {
            exception = new MissingOptionException("KeySerializer");
            return false;
        }
        if (ValueSerializer == null)
        {
            exception = new MissingOptionException("ValueSerializer");
            return false;
        }

        if (Comparer == null)
        {
            exception = new MissingOptionException("Comparer");
            return false;
        }

        if (RandomAccessDeviceManager == null)
        {
            exception = new MissingOptionException("RandomAccessDeviceManager");
            return false;
        }

        if (WriteAheadLogProvider == null)
        {
            exception = new MissingOptionException("WriteAheadLogProvider");
            return false;
        }
        exception = null;
        return true;
    }

    public void Validate()
    {
        if (!TryValidate(out Exception exception))
            throw exception;
    }

    public IWriteAheadLogProvider WriteAheadLogProvider { get; set; }

    public IRandomAccessDeviceManager RandomAccessDeviceManager { get; set; }
}
