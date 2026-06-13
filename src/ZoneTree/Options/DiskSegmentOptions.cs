namespace ZoneTree.Options;

/// <summary>
/// Represents the configuration options for disk segments in the ZoneTree.
/// </summary>
public sealed class DiskSegmentOptions
{
  /// <summary>
  /// Gets or sets the mode for the disk segment.
  /// Default value is <see cref="DiskSegmentMode.MultiPartDiskSegment"/>.
  /// </summary>
  public DiskSegmentMode DiskSegmentMode { get; set; }
      = DiskSegmentDefaultValues.DiskSegmentMode;

  /// <summary>
  /// Gets or sets the block size for disk segment compression, in bytes.
  /// Larger blocks may improve compression ratio, but each block is loaded and
  /// decompressed as a unit, increasing memory usage and read latency.
  /// Default value is 4 MB (4 * 1024 * 1024 bytes).
  /// </summary>
  public int CompressionBlockSize { get; set; } = DiskSegmentDefaultValues.CompressionBlockSize;

  /// <summary>
  /// Gets or sets the compression method used if compression is enabled.
  /// Default value is <see cref="CompressionMethod.Zstd"/>.
  /// </summary>
  public CompressionMethod CompressionMethod { get; set; } = DiskSegmentDefaultValues.CompressionMethod;

  /// <summary>
  /// Gets or sets the compression level for the selected compression method.
  /// Default value is <see cref="CompressionLevels.Zstd0"/>.
  /// </summary>
  public int CompressionLevel { get; set; } = DiskSegmentDefaultValues.CompressionLevel;

  /// <summary>
  /// Gets or sets the upper target record count for multipart disk segment
  /// parts. Multipart parts bound the amount of persistent data ZoneTree may
  /// need to rewrite when a local key range changes. Larger values create
  /// larger parts, which can reduce file count and improve sequential scan
  /// behavior, but a changed range may rewrite more records.
  /// ZoneTree randomizes new part targets between
  /// <see cref="MinimumRecordCount"/> and <see cref="MaximumRecordCount"/> to
  /// avoid rigid, fully packed part boundaries and keep part reuse effective
  /// over time. This option is used only when
  /// <see cref="DiskSegmentMode.MultiPartDiskSegment"/> is enabled.
  /// Default value is 3M records.
  /// </summary>
  public int MaximumRecordCount { get; set; } = DiskSegmentDefaultValues.MaximumRecordCount;

  /// <summary>
  /// Gets or sets the lower target record count for multipart disk segment
  /// parts. Smaller values create finer rewrite units, which can reduce the
  /// amount of data rewritten for localized changes and large records. Very
  /// small values can create too many parts, increasing file count, metadata
  /// size, iterator transitions, backup overhead, and sequential scan cost.
  /// During merge, existing parts at or below this size are merged instead of
  /// reused, keeping the multipart shape from fragmenting into tiny parts.
  /// This option is used only when
  /// <see cref="DiskSegmentMode.MultiPartDiskSegment"/> is enabled.
  /// Default value is 1.5M records.
  /// </summary>
  public int MinimumRecordCount { get; set; } = DiskSegmentDefaultValues.MinimumRecordCount;

  /// <summary>
  /// Gets or sets the size of the circular buffer cache for keys.
  /// This cache is checked before accessing the block cache during lookups and searches.
  /// Larger values keep more keys in memory.
  /// Default value is 1024.
  /// </summary>
  public int KeyCacheSize { get; set; } = DiskSegmentDefaultValues.KeyCacheSize;

  /// <summary>
  /// Gets or sets the size of the circular buffer cache for values.
  /// This cache is checked before accessing the block cache during lookups and searches.
  /// Larger values keep more values in memory.
  /// Default value is 1024.
  /// </summary>
  public int ValueCacheSize { get; set; } = DiskSegmentDefaultValues.ValueCacheSize;

  /// <summary>
  /// Gets or sets the maximum lifetime of a record in the key cache, in milliseconds.
  /// Longer lifetimes can keep cached keys in memory for longer.
  /// Default value is 10,000 milliseconds (10 seconds).
  /// </summary>
  public int KeyCacheRecordLifeTimeInMillisecond { get; set; } = DiskSegmentDefaultValues.KeyCacheRecordLifeTimeInMillisecond;

  /// <summary>
  /// Gets or sets the maximum lifetime of a record in the value cache, in milliseconds.
  /// Longer lifetimes can keep cached values in memory for longer.
  /// Default value is 10,000 milliseconds (10 seconds).
  /// </summary>
  public int ValueCacheRecordLifeTimeInMillisecond { get; set; } = DiskSegmentDefaultValues.ValueCacheRecordLifeTimeInMillisecond;

  /// <summary>
  /// Gets or sets the default step size for the default sparse array of disk segments.
  /// Setting the step size to zero disables loading and creating the default sparse array.
  /// Smaller positive values create denser sparse arrays and can increase memory usage.
  /// Default value is 1024.
  /// </summary>
  public int DefaultSparseArrayStepSize { get; set; } = DiskSegmentDefaultValues.DefaultSparseArrayStepSize;
}
