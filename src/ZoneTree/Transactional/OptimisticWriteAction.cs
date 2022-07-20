namespace Tenray.ZoneTree.Transactional;

public enum OptimisticWriteAction
{
    SkipWrite,
    Write,
    Abort
}
