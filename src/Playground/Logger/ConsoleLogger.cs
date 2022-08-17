using Microsoft.Extensions.Logging;

namespace Tenray.ZoneTree.Core;

public class ConsoleLogger : ILogger
{
    public static LogLevel DefaultLogLevel = LogLevel.Warning;

    public LogLevel LogLevel { get; set; }

    public ConsoleLogger()
    {
        LogLevel = DefaultLogLevel;
    }

    public ConsoleLogger(LogLevel logLevel)
    {
        LogLevel = logLevel;
    }

    private void LogError(Exception log)
    {
        lock (this)
        {
            var existing = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(log.ToString());
            Console.ForegroundColor = existing;
        }
    }

    private void LogInfo(object log)
    {
        if (LogLevel > LogLevel.Information)
            return;
        lock (this)
        {
            var existing = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(log.ToString());
            Console.ForegroundColor = existing;
        }
    }

    private void LogTrace(object log)
    {
        if (LogLevel > LogLevel.Trace)
            return;
        lock (this)
        {
            var existing = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(log.ToString());
            Console.ForegroundColor = existing;
        }
    }

    private void LogWarning(object log)
    {
        if (LogLevel > LogLevel.Warning)
            return;
        lock (this)
        {
            var existing = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(log.ToString());
            Console.ForegroundColor = existing;
        }
    }

    public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        switch (logLevel)
        {
            case Microsoft.Extensions.Logging.LogLevel.Trace:
                LogTrace(state);
                break;
            case Microsoft.Extensions.Logging.LogLevel.Debug:
                LogTrace(state);
                break;
            case Microsoft.Extensions.Logging.LogLevel.Information:
                LogInfo(state);
                break;
            case Microsoft.Extensions.Logging.LogLevel.Warning:
                LogWarning(state);
                break;
            case Microsoft.Extensions.Logging.LogLevel.Error:
                LogError(exception);
                break;
            case Microsoft.Extensions.Logging.LogLevel.Critical:
                LogError(exception);
                break;
            case Microsoft.Extensions.Logging.LogLevel.None:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null);
        }
    }

    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel)
    {
        return true;
    }

    public IDisposable BeginScope<TState>(TState state)
    {
        throw new NotImplementedException();
    }
}