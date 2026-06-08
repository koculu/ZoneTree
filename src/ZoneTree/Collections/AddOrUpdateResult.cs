namespace ZoneTree.Collections;

public enum AddOrUpdateResult
{
  ADDED,
  UPDATED,
  RETRY_SEGMENT_IS_FULL,
  RETRY_SEGMENT_IS_FROZEN
}
