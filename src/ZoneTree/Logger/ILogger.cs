namespace Tenray.ZoneTree.Logger;

/// <summary>
/// The logger interface for ZoneTree.
/// You can create a wrapper Logger class to integrate with any
/// logging library.
/// </summary>
public interface ILogger
{
    /// <summary>
    /// Sets or gets the current log level.
    /// </summary>
    public LogLevel LogLevel { get; set; }

    /// <summary>
    /// Logs an error.
    /// </summary>
    /// <param name="log"></param>
    public void LogError(Exception log);

    /// <summary>
    /// Logs a warning.
    /// </summary>
    /// <param name="log"></param>
    public void LogWarning(object log);

    /// <summary>
    /// Logs an information.
    /// </summary>
    /// <param name="log"></param>
    public void LogInfo(object log);

    /// <summary>
    /// Logs a trace information.
    /// </summary>
    /// <param name="log"></param>
    public void LogTrace(object log);
}
