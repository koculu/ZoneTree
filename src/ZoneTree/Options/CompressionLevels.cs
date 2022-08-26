namespace Tenray.ZoneTree;

/// <summary>
/// Compression levels for available compression methods.
/// </summary>
/// <remarks>
/// The zstd compression library provides in-memory compression
/// and decompression functions.
/// The library supports regular compression levels from 1 up to ZSTD_maxCLevel(),
/// which is currently 22. Levels >= 20, labeled `--ultra`, should be used with
/// caution, as they require more memory. The library also offers negative
/// compression levels, which extend the range of speed vs.ratio preferences.
/// The lower the level, the faster the speed (at the cost of compression).
/// </remarks>
public static class CompressionLevels
{
    public const int GzipOptimal = 0;
    public const int GzipFastest = 1;
    public const int GzipNoCompression = 2;
    public const int GzipSmallestSize = 3;

    public const int BrotliOptimal = 0;
    public const int BrotliFastest = 1;
    public const int BrotliNoCompression = 2;
    public const int BrotliSmallestSize = 3;

    public const int LZ4Fastest = 0;
    public const int LZ4SmallestSize = 1;


    public const int ZstdMin = -131072;
    public const int Zstd0 = 0;
    public const int Zstd1 = 1;
    public const int Zstd2 = 2;
    public const int Zstd3 = 3;
    public const int Zstd4 = 4;
    public const int Zstd5 = 5;
    public const int ZstdMax = 22;
}