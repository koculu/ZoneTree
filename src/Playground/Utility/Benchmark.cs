using Humanizer;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Playground.Benchmark;

public class Benchmark
{
    public List<IStatsCollector> StatsCollectors { get; } = new();
    
    public string CurrentSection { get; private set; }

    public IStatsCollector Run(Action<IStatsCollector> action)
    {
        GC.Collect();
        var collector = new StatsCollector(CurrentSection);
        action(collector);
        collector.End();
        collector.MemoryUsageAtEnd = GC.GetTotalMemory(false);
        collector.LogWithColor("Memory At End", 
            collector.MemoryUsageAtEnd.Bytes().Humanize(), ConsoleColor.DarkGreen);
        StatsCollectors.Add(collector);
        GC.Collect();
        return collector;
    }

    public string ToJSON()
    {
        return JsonSerializer.Serialize(StatsCollectors, GetJSONOptions());
    }

    public static JsonSerializerOptions GetJSONOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            ReferenceHandler = ReferenceHandler.IgnoreCycles
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    public void NewSection(string section)
    {
        var sc = new StatsCollector();
        sc.LogLine();
        CurrentSection = section;
        sc.LogWithColor(section, ConsoleColor.Red);
        sc.LogLine();
    }
}