using Tenray.ZoneTree.WAL;

namespace Playground.Benchmark;

public static class BenchmarkGroups
{
    static int[] TestCounts = new[] {
        1_000_000,
        2_000_000,
        3_000_000,
        /*4_000_000,
        5_000_000,
        6_000_000,
        7_000_000,
        8_000_000,
        9_000_000,*/
        10_000_000
    };

    static WriteAheadLogMode[] TestWALModes = new[] {
        WriteAheadLogMode.None,
        WriteAheadLogMode.Lazy,
        WriteAheadLogMode.CompressedImmediate,
        WriteAheadLogMode.Immediate
    };

    static void Run(int count, WriteAheadLogMode? mode, Action<WriteAheadLogMode, int> job)
    {
        var counts = TestCounts.ToList();
        if (count != 0)
        {
            counts.Clear();
            counts.Add(count);
        }

        var modes = TestWALModes.ToList();
        if (mode.HasValue)
        {
            modes.Clear();
            modes.Add(mode.Value);
        }
        foreach (var m in modes)
        {
            foreach (var c in counts)
            {
                job(m, c);                
            }
        }
    }

    public static void InsertIterate1(int count = 0, WriteAheadLogMode? mode = null)
    {
        Run(count, mode, (m, c) =>
        {
            ZoneTree1.Insert(m, c);
            ZoneTree1.Iterate(m, c);
        });
    }
    public static void InsertIterate2(int count = 0, WriteAheadLogMode? mode = null)
    {
        Run(count, mode, (m, c) =>
        {
            ZoneTree2.Insert(m, c);
            ZoneTree2.Iterate(m, c);
        });
    }

    public static void LogWithColor(string msg, ConsoleColor color)
    {
        var existingColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(msg);
        Console.ForegroundColor = existingColor;
    }

    public static void LogWithColor(string msg, long duration, ConsoleColor color)
    {
        Console.Write(msg + " ");
        var existingColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(duration);
        Console.ForegroundColor = existingColor;
    }

    public static void InsertIterate3(int count = 0, WriteAheadLogMode? mode = null)
    {
        Run(count, mode, (m, c) =>
        {
            ZoneTree3.Insert(m, c);
            ZoneTree3.Iterate(m, c);
        });
    }

    public static void Insert1(int count = 0, WriteAheadLogMode? mode = null)
    {
        Run(count, mode, (m,c) => ZoneTree1.Insert(m, c));
    }

    public static void Insert2(int count = 0, WriteAheadLogMode? mode = null)
    {
        Run(count, mode, (m, c) => ZoneTree2.Insert(m, c));
    }

    public static void Insert3(int count = 0, WriteAheadLogMode? mode = null)
    {
        Run(count, mode, (m, c) => ZoneTree3.Insert(m, c));
    }

    public static void Iterate1(int count = 0, WriteAheadLogMode? mode = null)
    {
        Run(count, mode, (m, c) => ZoneTree1.Iterate(m, c));
    }

    public static void Iterate2(int count = 0, WriteAheadLogMode? mode = null)
    {
        Run(count, mode, (m, c) => ZoneTree2.Iterate(m, c));
    }

    public static void Iterate3(int count = 0, WriteAheadLogMode? mode = null)
    {
        Run(count, mode, (m, c) => ZoneTree3.Iterate(m, c));
    }
}
