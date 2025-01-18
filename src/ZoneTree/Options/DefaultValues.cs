using System.Collections.ObjectModel;
using Tenray.ZoneTree.Collections.BTree.Lock;

namespace Tenray.ZoneTree.Options;

public static class DefaultValues
{
    public static readonly int MutableSegmentMaxItemCount = 1_000_000;

    public static readonly int DiskSegmentMaxItemCount = 20_000_000;

    public static readonly BTreeLockMode BTreeLockMode = BTreeLockMode.NodeLevelMonitor;

    public static readonly int BTreeNodeSize = 128;

    public static readonly int BTreeLeafSize = 128;
}