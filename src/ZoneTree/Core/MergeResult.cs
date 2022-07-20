namespace Tenray.ZoneTree.Core;

public enum MergeResult
{
    SUCCESS,
    RETRY_READONLY_SEGMENTS_ARE_NOT_READY,
    NOTHING_TO_MERGE,
    CANCELLED_BY_USER,
    ANOTHER_MERGE_IS_RUNNING
}