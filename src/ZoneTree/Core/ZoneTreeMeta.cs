using Tenray.ZoneTree.Options;
using Tenray.ZoneTree.WAL;

namespace Tenray.ZoneTree.Core;

public class ZoneTreeMeta
{
    public string Version { get; set; }

    public string ComparerType { get; set; }

    public string KeyType { get; set; }

    public string ValueType { get; set; }

    public string KeySerializerType { get; set; }

    public string ValueSerializerType { get; set; }

    public int MutableSegmentMaxItemCount { get; set; }

    public WriteAheadLogOptions WriteAheadLogOptions { get; set; }
    
    public DiskSegmentOptions DiskSegmentOptions { get; set; }

    public long SegmentZero { get; set; }

    public IReadOnlyList<long> ReadOnlySegments { get; set; }

    public long DiskSegment { get; set; }
}
