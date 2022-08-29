namespace Playground.Benchmark;

public interface IStatsCollector
{
    public string Section { get; }

    string Name { get; set; }

    IDictionary<string, object> Options { get; }

    IDictionary<string, object> AdditionalStats { get; }

    StageMap Stages { get; }

    long MemoryUsageAtBegin { get; }

    long MemoryUsageAtEnd { get; }

    void AddStage(string name, ConsoleColor color = ConsoleColor.DarkYellow);

    void SetOption(string key, object value);

    void AddAdditionalStats(string name, object additionalStats, ConsoleColor color = ConsoleColor.DarkYellow);

    void RestartStopwatch();

    string ToJson();

    void LogLine();

    void LogWithColor(string msg, ConsoleColor color, bool newLine = true);
    
    void LogWithColor(string key, object value, ConsoleColor color);
}
