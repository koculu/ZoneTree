using Tenray.ZoneTree.WAL;

namespace Playground.Benchmark;

public static class BenchmarkGroups
{
    static int[] TestCounts = new[] {
        1_000_000,
        2_000_000,
        3_000_000,
        10_000_000
    };

    static WriteAheadLogMode[] TestWALModes = new[] {
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
    public static void InsertBenchmark1(int count = 0, WriteAheadLogMode? mode = null)
    {
        Run(count, mode, (m,c) => ZoneTree1.TestInsertIntTree(m, c));
    }

    public static void InsertBenchmark2(int count = 0, WriteAheadLogMode? mode = null)
    {
        Run(count, mode, (m, c) => ZoneTree2.TestInsertStringTree(m, c));
    }

    public static void InsertBenchmark3(int count = 0, WriteAheadLogMode? mode = null)
    {
        Run(count, mode, (m, c) => ZoneTree3.TestInsertTransactionIntTree(m, c));
    }

    public static void LoadAndIterateBenchmark1(int count = 0, WriteAheadLogMode? mode = null)
    {
        Run(count, mode, (m, c) => ZoneTree1.TestIterateIntTree(m, c));
    }

    public static void LoadAndIterateBenchmark2(int count = 0, WriteAheadLogMode? mode = null)
    {
        Run(count, mode, (m, c) => ZoneTree2.TestIterateStringTree(m, c));
    }

    public static void LoadAndIterateBenchmark3(int count = 0, WriteAheadLogMode? mode = null)
    {
        Run(count, mode, (m, c) => ZoneTree3.TestIterateIntTree(m, c));
    }
}
