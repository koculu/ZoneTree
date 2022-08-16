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

    public void LogError(Exception log)
    {
        lock (this)
        {
            var existing = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(log.ToString());
            Console.ForegroundColor = existing;
        }
    }

    public void LogInfo(object log)
    {
        if (LogLevel > LogLevel.Info)
            return;
        lock (this)
        {
            var existing = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(log.ToString());
            Console.ForegroundColor = existing;
        }
    }

    public void LogTrace(object log)
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

    public void LogWarning(object log)
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
}
