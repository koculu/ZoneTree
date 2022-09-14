using Tenray.ZoneTree.Options;

namespace Tenray.ZoneTree.Core;

public sealed class ZoneTreeMeta
{
    public string Version { get; set; }

    public string ComparerType { get; set; }

    public string KeyType { get; set; }

    public string ValueType { get; set; }

    public string KeySerializerType { get; set; }

    public string ValueSerializerType { get; set; }

    public int MutableSegmentMaxItemCount { get; set; }

    public int DiskSegmentMaxItemCount { get; set; } = 20_000_000;

    public WriteAheadLogOptions WriteAheadLogOptions { get; set; }
    
    public DiskSegmentOptions DiskSegmentOptions { get; set; }

    public long MutableSegment { get; set; }

    public IReadOnlyList<long> ReadOnlySegments { get; set; }

    public long DiskSegment { get; set; }

    public IReadOnlyList<long> BottomSegments { get; set; }
}
