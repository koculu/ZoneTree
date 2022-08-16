using Tenray.ZoneTree.Core;

namespace Tenray.ZoneTree;

public interface ILogger
{
    public LogLevel LogLevel { get; set; }

    public void LogError(Exception log);

    public void LogWarning(object log);

    public void LogInfo(object log);

    public void LogTrace(object log);
}
