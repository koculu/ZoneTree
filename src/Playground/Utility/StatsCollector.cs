using Humanizer;
using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Playground.Benchmark;

public class StatsCollector : IStatsCollector
{
    public string Section { get; }

    public Stopwatch Stopwatch { get; }
    
    public string Name { get; set; }

    public long MemoryUsageAtBegin { get; set; }

    public long MemoryUsageAtEnd { get; set; }

    public IDictionary<string, object> Options { get; } = new Dictionary<string, object>();

    public StageMap Stages { get; } = new();

    public IDictionary<string, object> AdditionalStats { get; } = new Dictionary<string, object>();

    public StatsCollector(string section)
    {
        Stopwatch = new Stopwatch();
        Stopwatch.Start();
        MemoryUsageAtBegin = GC.GetTotalMemory(true);
        LogLine();
        LogWithColor("Memory At Begin", 
            MemoryUsageAtBegin.Bytes().Humanize(),
            ConsoleColor.DarkYellow);
        Section = section ?? "DefaultJob";
    }

    public StatsCollector()
    {
    }

    public void End()
    {
        Stopwatch.Stop();
    }

    public void AddStage(string name, ConsoleColor color)
    {
        var stage = new Stage
        {
            Name = name,
            ElapsedMilliseconds = Stopwatch.ElapsedMilliseconds
        };
        LogWithColor(
            name,
            stage.ElapsedMilliseconds,
            color);
        Stages.Add(name, stage);
        Stopwatch.Restart();

    }

    public void SetOption(string key, object value)
    {
        Options.Add(key, value);
    }

    public void AddAdditionalStats(string name, object additionalStats, ConsoleColor color = ConsoleColor.DarkYellow)
    {
        AdditionalStats.Add(name, additionalStats);
        LogWithColor(
            name,
            additionalStats?.ToString(),
            color);
    }

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, Benchmark.GetJSONOptions());
    }

    public void RestartStopwatch()
    {
        Stopwatch.Restart();
    }

    public void LogLine()
    {
        Console.WriteLine("----------------------------------");
    }

    public void LogWithColor(string msg, ConsoleColor color, bool newLine = true)
    {
        var existingColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        if (newLine)
            Console.WriteLine(msg);
        else
            Console.Write(msg);
        Console.ForegroundColor = existingColor;
    }

    public void LogWithColor(string key, object value, ConsoleColor color)
    {
        Console.Write(key + ": ");
        var existingColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(value);
        Console.ForegroundColor = existingColor;
    }
}
