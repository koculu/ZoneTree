namespace Tenray.ZoneTree;

/// <summary>
/// Available ZoneTree Iterator Types.
/// </summary>
public enum IteratorType
{
    /// <summary>
    /// AutoRefresh iterator iterates through all available segments.
    /// It also includes newly inserted segments after any MoveMutableSegment.
    /// Newly inserted keys might not be included in the iteration if the iterator position advanced 
    /// newly inserted positions.
    /// </summary>
    AutoRefresh,

    /// <summary>
    /// Snapshot iterator initiates MoveMutableSegmentForward first when it is created.
    /// After that, it fetches all available read-only segments.
    /// Since it does not collect the mutable segment
    /// the iterator is consistent across its lifetime.
    /// It does not see new writes to the database.
    /// </summary>
    Snapshot,

    /// <summary>
    /// It is like Snapshot iterator but does not initiate MoveMutableSegmentForward.
    /// It fetches all available read-only segments.
    /// Since it does not contain the mutable segment
    /// the iterator is consistent across its lifetime.
    /// It does not see new writes to the database.
    /// You may manually call MoveMutableSegmentForward before creating the iterator,
    /// to ensure the current mutable segment's content
    /// is also included in the iteration.
    /// </summary>
    ReadOnlyRegion,

    /// <summary>
    /// NoRefresh iterator iterates through all available segments.
    /// It does not include newly inserted segments after a new MoveMutableSegmentForward event.
    /// It can fetch newly inserted records to the current mutable segment.
    /// Newly inserted keys might not be included in the iteration if the iterator position advances 
    /// newly inserted positions.
    /// Iterator can be manually refreshed.
    /// </summary>
    NoRefresh,
}
