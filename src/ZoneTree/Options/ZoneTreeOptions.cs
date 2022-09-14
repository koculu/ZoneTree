using Tenray.ZoneTree.Exceptions;
using Tenray.ZoneTree.Segments.Disk;
using Tenray.ZoneTree.WAL;
using Tenray.ZoneTree.Collections.BTree.Lock;
using Tenray.ZoneTree.Logger;
using Tenray.ZoneTree.Serializers;
using Tenray.ZoneTree.Comparers;

namespace Tenray.ZoneTree.Options;

/// <summary>
/// A delegate to query value deletion state.
/// </summary>
/// <typeparam name="TValue">The value type</typeparam>
/// <param name="value">Value to be queried</param>
/// <returns>true if value is deleted, false otherwise</returns>
public delegate bool IsValueDeletedDelegate<TValue>(in TValue value);

/// <summary>
/// A delegate to mark a value deleted.
/// </summary>
/// <typeparam name="TValue">The value type</typeparam>
/// <param name="value">The value to be marked as deleted</param>
public delegate void MarkValueDeletedDelegate<TValue>(ref TValue value);

/// <summary>
/// Represents configuration options of a ZoneTree.
/// </summary>
/// <typeparam name="TKey">The key type</typeparam>
/// <typeparam name="TValue">The value type</typeparam>
public sealed class ZoneTreeOptions<TKey, TValue>
{
    /// <summary>
    /// Mutable segment maximumum key-value pair count.
    /// When the maximum count is reached 
    /// MoveMutableSegmentForward is called and current mutable segment is enqueued to
    /// the ReadOnlySegments layer.
    /// </summary>
    public int MutableSegmentMaxItemCount { get; set; } = 1_000_000;

    /// <summary>
    /// Disk segment maximumum key-value pair count.
    /// When the maximum count is reached
    /// The disk segment is enqueued into to the bottom segments layer.
    /// </summary>
    public int DiskSegmentMaxItemCount { get; set; } = 20_000_000;

    /// <summary>
    /// The key comparer.
    /// </summary>
    public IRefComparer<TKey> Comparer { get; set; }

    /// <summary>
    /// The key serializer.
    /// </summary>
    public ISerializer<TKey> KeySerializer { get; set; }

    /// <summary>
    /// The value serializer.
    /// </summary>
    public ISerializer<TValue> ValueSerializer { get; set; }

    /// <summary>
    /// Delegate to query value deletion state.
    /// </summary>
    public IsValueDeletedDelegate<TValue> IsValueDeleted { get; set; } = (in TValue x) => false;

    /// <summary>
    /// Delegate to mark value deleted.
    /// </summary>
    public MarkValueDeletedDelegate<TValue> MarkValueDeleted { get; set; } = (ref TValue x) => { x = default; };

    /// <summary>
    /// Write Ahead Log Options. The options are used
    /// to create new Write Ahead Logs.
    /// Existing WALs are created with their existing options.
    /// </summary>
    public WriteAheadLogOptions WriteAheadLogOptions { get; set; } = new();

    /// <summary>
    /// Disk Segment options. The options are used 
    /// to create new disk segments.
    /// Existing disk segments are created with
    /// their existing options.
    /// </summary>
    public DiskSegmentOptions DiskSegmentOptions { get; set; } = new();

    /// <summary>
    /// Controls lock granularity of in-memory BTree that represents the
    /// mutable segment.
    /// </summary>
    public BTreeLockMode BTreeLockMode { get; set; } = BTreeLockMode.NodeLevelMonitor;

    /// <summary>
    /// ZoneTree Logger.
    /// </summary>
    public ILogger Logger { get; set; } = new ConsoleLogger();

    /// <summary>
    /// Tries validate ZoneTree options without throwing an exception.
    /// </summary>
    /// <param name="exception">The exception outcome of validation.</param>
    /// <returns>true if validation succeeds, false otherwise.</returns>
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

        exception = ValidateCompressionLevel(
            "disk segment", 
            DiskSegmentOptions.CompressionMethod, 
            DiskSegmentOptions.CompressionLevel);
        
        if (exception != null)
            return false;

        exception = ValidateCompressionLevel(
            "write ahead log",
            WriteAheadLogOptions.CompressionMethod,
            WriteAheadLogOptions.CompressionLevel);

        if (exception != null)
            return false;

        exception = null;
        return true;
    }

    static Exception ValidateCompressionLevel(
        string option, 
        CompressionMethod method, 
        int level)
    {
        var exception = new CompressionLevelIsOutOfRangeException
            (option, method, level);
        return method switch
        {
            CompressionMethod.None => null,
            CompressionMethod.Gzip => 
                (level >= CompressionLevels.GzipOptimal &&
                level <= CompressionLevels.GzipSmallestSize) ?
                null : exception,
            CompressionMethod.LZ4 => 
                (level >= 0 && level <= 12 && level != 1 && level != 2) ? null : exception,
            CompressionMethod.Zstd => 
                (level >= CompressionLevels.ZstdMin &&
                level <= CompressionLevels.ZstdMax) ?
                null : exception,
            CompressionMethod.Brotli =>
                (level >= CompressionLevels.BrotliOptimal &&
                level <= CompressionLevels.BrotliSmallestSize) ?
                null : exception,
            _ => null,
        };
    }

    /// <summary>
    /// Validates the ZoneTree options.
    /// </summary>
    public void Validate()
    {
        if (!TryValidate(out Exception exception))
            throw exception;
    }

    /// <summary>
    /// Configures the write ahead log provider.
    /// </summary>
    public IWriteAheadLogProvider WriteAheadLogProvider { get; set; }

    /// <summary>
    /// Configures the random access device manager.
    /// </summary>
    public IRandomAccessDeviceManager RandomAccessDeviceManager { get; set; }
}
