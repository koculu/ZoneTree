namespace ZoneTree.Core;

public class ZoneTreeMeta
{
    public string ComparerType { get; set; }

    public string KeyType { get; set; }

    public string ValueType { get; set; }

    public string KeySerializerType { get; set; }

    public string ValueSerializerType { get; set; }

    public int SegmentZero { get; set; }

    public IReadOnlyList<int> ReadOnlySegments { get; set; }

    public int DiskSegment { get; set; }
}
