#undef TRACE_ENABLED


namespace Tenray.ZoneTree.Core;

public interface IMaintainer : IDisposable
{
    void TryCancelRunningTasks();

    void CompleteRunningTasks();
}
