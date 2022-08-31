namespace Tenray.ZoneTree.Options;

/// <summary>
/// Available compression methods.
/// </summary>
/// <remarks>
/// See <see cref="CompressionLevels"/> for supported compression levels.
/// </remarks>
public enum CompressionMethod : byte
{
    /// <summary>
    /// LZ4 algorithm.
    /// </summary>
    LZ4 = 0,

    /// <summary>
    /// ZStd algorithm.
    /// </summary>
    Zstd = 1,

    /// <summary>
    /// Brotli algorithm.
    /// </summary>
    Brotli = 2,

    /// <summary>
    /// Gzip algorithm.
    /// </summary>
    Gzip = 3,

    /// <summary>
    /// No compression.
    /// </summary>
    None = 4,
}
