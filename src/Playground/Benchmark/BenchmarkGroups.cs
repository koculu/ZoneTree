using Tenray.ZoneTree.WAL;

namespace Playground.Benchmark;

public static class BenchmarkGroups
{
    public static void InsertBenchmark1()
    {
        ZoneTree1.TestInsertIntTree(WriteAheadLogMode.Immediate, 1_000_000);
        ZoneTree1.TestInsertIntTree(WriteAheadLogMode.Immediate, 2_000_000);
        ZoneTree1.TestInsertIntTree(WriteAheadLogMode.Immediate, 3_000_000);
        ZoneTree1.TestInsertIntTree(WriteAheadLogMode.Immediate, 10_000_000);
        ZoneTree1.TestInsertIntTree(WriteAheadLogMode.Lazy, 1_000_000);
        ZoneTree1.TestInsertIntTree(WriteAheadLogMode.Lazy, 2_000_000);
        ZoneTree1.TestInsertIntTree(WriteAheadLogMode.Lazy, 3_000_000);
        ZoneTree1.TestInsertIntTree(WriteAheadLogMode.Lazy, 10_000_000);
    }

    public static void InsertBenchmark2()
    {
        ZoneTree2.TestInsertStringTree(WriteAheadLogMode.Immediate, 1_000_000);
        ZoneTree2.TestInsertStringTree(WriteAheadLogMode.Immediate, 2_000_000);
        ZoneTree2.TestInsertStringTree(WriteAheadLogMode.Immediate, 3_000_000);
        ZoneTree2.TestInsertStringTree(WriteAheadLogMode.Immediate, 10_000_000);
        ZoneTree2.TestInsertStringTree(WriteAheadLogMode.Lazy, 1_000_000);
        ZoneTree2.TestInsertStringTree(WriteAheadLogMode.Lazy, 2_000_000);
        ZoneTree2.TestInsertStringTree(WriteAheadLogMode.Lazy, 3_000_000);
        ZoneTree2.TestInsertStringTree(WriteAheadLogMode.Lazy, 10_000_000);
    }

    public static void InsertBenchmark3()
    {
        ZoneTree3.TestInsertTransactionIntTree(WriteAheadLogMode.Immediate, 1_000_000);
        ZoneTree3.TestInsertTransactionIntTree(WriteAheadLogMode.Immediate, 2_000_000);
        ZoneTree3.TestInsertTransactionIntTree(WriteAheadLogMode.Immediate, 3_000_000);
        ZoneTree3.TestInsertTransactionIntTree(WriteAheadLogMode.Immediate, 10_000_000);
        ZoneTree3.TestInsertTransactionIntTree(WriteAheadLogMode.Lazy, 1_000_000);
        ZoneTree3.TestInsertTransactionIntTree(WriteAheadLogMode.Lazy, 2_000_000);
        ZoneTree3.TestInsertTransactionIntTree(WriteAheadLogMode.Lazy, 3_000_000);
        ZoneTree3.TestInsertTransactionIntTree(WriteAheadLogMode.Lazy, 10_000_000);
    }

    public static void LoadAndIterateBenchmark1()
    {
        ZoneTree1.TestInsertIntTree(WriteAheadLogMode.Immediate, 1_000_000);
        ZoneTree1.TestInsertIntTree(WriteAheadLogMode.Immediate, 2_000_000);
        ZoneTree1.TestInsertIntTree(WriteAheadLogMode.Immediate, 3_000_000);
        ZoneTree1.TestInsertIntTree(WriteAheadLogMode.Immediate, 10_000_000);
        ZoneTree1.TestInsertIntTree(WriteAheadLogMode.Lazy, 1_000_000);
        ZoneTree1.TestInsertIntTree(WriteAheadLogMode.Lazy, 2_000_000);
        ZoneTree1.TestInsertIntTree(WriteAheadLogMode.Lazy, 3_000_000);
        ZoneTree1.TestInsertIntTree(WriteAheadLogMode.Lazy, 10_000_000);
    }

    public static void LoadAndIterateBenchmark2()
    {
        ZoneTree2.TestInsertStringTree(WriteAheadLogMode.Immediate, 1_000_000);
        ZoneTree2.TestInsertStringTree(WriteAheadLogMode.Immediate, 2_000_000);
        ZoneTree2.TestInsertStringTree(WriteAheadLogMode.Immediate, 3_000_000);
        ZoneTree2.TestInsertStringTree(WriteAheadLogMode.Immediate, 10_000_000);
        ZoneTree2.TestInsertStringTree(WriteAheadLogMode.Lazy, 1_000_000);
        ZoneTree2.TestInsertStringTree(WriteAheadLogMode.Lazy, 2_000_000);
        ZoneTree2.TestInsertStringTree(WriteAheadLogMode.Lazy, 3_000_000);
        ZoneTree2.TestInsertStringTree(WriteAheadLogMode.Lazy, 10_000_000);
    }

    public static void LoadAndIterateBenchmark3()
    {
        ZoneTree3.TestIterateIntTree(WriteAheadLogMode.Immediate, 1_000_000);
        ZoneTree3.TestIterateIntTree(WriteAheadLogMode.Immediate, 2_000_000);
        ZoneTree3.TestIterateIntTree(WriteAheadLogMode.Immediate, 3_000_000);
        ZoneTree3.TestIterateIntTree(WriteAheadLogMode.Immediate, 10_000_000);
        ZoneTree3.TestIterateIntTree(WriteAheadLogMode.Lazy, 1_000_000);
        ZoneTree3.TestIterateIntTree(WriteAheadLogMode.Lazy, 2_000_000);
        ZoneTree3.TestIterateIntTree(WriteAheadLogMode.Lazy, 3_000_000);
        ZoneTree3.TestIterateIntTree(WriteAheadLogMode.Lazy, 10_000_000);
    }
}
