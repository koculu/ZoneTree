namespace Tenray.ZoneTree;

/// <summary>
/// The enumeration for merge results.
/// </summary>
public enum MergeResult
{
    SUCCESS,
    RETRY_READONLY_SEGMENTS_ARE_NOT_READY,
    NOTHING_TO_MERGE,
    CANCELLED_BY_USER,
    ANOTHER_MERGE_IS_RUNNING,
    FAILURE,
}