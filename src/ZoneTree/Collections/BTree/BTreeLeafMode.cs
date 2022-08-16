namespace Tenray.ZoneTree.Collections.BTree;

/// <summary>
/// Available BTree leaf node modes.
/// </summary>
public enum BTreeLeafMode
{
    /// <summary>
    /// Leafs contain keys and values.
    /// With this leaf mode,
    /// snapshot iterators moves SegmentZero forward upon creation.
    /// </summary>
    Default,

    /// <summary>
    /// Leafs contain keys, values and opIndexes.
    /// It enables the snapshot iterator creation without mutable segment clearance.
    /// Trade-off is slightly increased memory consumption and
    /// slight performance loss on inserts and updates.
    /// With this leaf mode,
    /// snapshot iterator can utilize the OpIndexes 
    /// to retrieve the key and values that belong
    /// to the snapshot.
    /// </summary>
    LeafWithOpIndex
}