using Tenray.ZoneTree.WAL;

namespace Tenray.ZoneTree.Core;

public class ZoneTreeMeta
{
    public string ComparerType { get; set; }

    public string KeyType { get; set; }

    public string ValueType { get; set; }

    public string KeySerializerType { get; set; }

    public string ValueSerializerType { get; set; }

    public WriteAheadLogMode WriteAheadLogMode { get; set; }
    
    public bool EnableDiskSegmentCompression { get; set; }

    public int DiskSegmentCompressionBlockSize { get; set; }

    public int SegmentZero { get; set; }

    public IReadOnlyList<int> ReadOnlySegments { get; set; }

    public int DiskSegment { get; set; }
}
