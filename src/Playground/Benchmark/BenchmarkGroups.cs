using Tenray.ZoneTree.WAL;

namespace Playground.Benchmark;

public static class BenchmarkGroups
{
    public static void InsertBenchmark()
    {
        ZoneTree1.TestIntTree(WriteAheadLogMode.Immediate, 1_000_000);
        ZoneTree1.TestIntTree(WriteAheadLogMode.Immediate, 2_000_000);
        ZoneTree1.TestIntTree(WriteAheadLogMode.Immediate, 3_000_000);
        ZoneTree1.TestIntTree(WriteAheadLogMode.Immediate, 10_000_000);
        ZoneTree1.TestIntTree(WriteAheadLogMode.Lazy, 1_000_000);
        ZoneTree1.TestIntTree(WriteAheadLogMode.Lazy, 2_000_000);
        ZoneTree1.TestIntTree(WriteAheadLogMode.Lazy, 3_000_000);
        ZoneTree1.TestIntTree(WriteAheadLogMode.Lazy, 10_000_000);

        ZoneTree2.TestStringTree(WriteAheadLogMode.Immediate, 1_000_000);
        ZoneTree2.TestStringTree(WriteAheadLogMode.Immediate, 2_000_000);
        ZoneTree2.TestStringTree(WriteAheadLogMode.Immediate, 3_000_000);
        ZoneTree2.TestStringTree(WriteAheadLogMode.Immediate, 10_000_000);
        ZoneTree2.TestStringTree(WriteAheadLogMode.Lazy, 1_000_000);
        ZoneTree2.TestStringTree(WriteAheadLogMode.Lazy, 2_000_000);
        ZoneTree2.TestStringTree(WriteAheadLogMode.Lazy, 3_000_000);
        ZoneTree2.TestStringTree(WriteAheadLogMode.Lazy, 10_000_000);
    }
}
