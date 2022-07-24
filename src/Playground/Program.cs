using Playground.Benchmark;
using Tenray.ZoneTree.WAL;

ZoneTree1.TestIntTree(WriteAheadLogMode.Immediate, 1_000_000);
ZoneTree1.TestIntTree(WriteAheadLogMode.Lazy, 1_000_000);
ZoneTree2.TestStringTree(WriteAheadLogMode.Immediate, 1_000_000);
ZoneTree2.TestStringTree(WriteAheadLogMode.Lazy, 1_000_000);

ZoneTree1.TestIntTree(WriteAheadLogMode.Immediate, 2_000_000);
ZoneTree1.TestIntTree(WriteAheadLogMode.Lazy, 2_000_000);
ZoneTree2.TestStringTree(WriteAheadLogMode.Immediate, 2_000_000);
ZoneTree2.TestStringTree(WriteAheadLogMode.Lazy, 2_000_000);

ZoneTree1.TestIntTree(WriteAheadLogMode.Immediate, 3_000_000);
ZoneTree1.TestIntTree(WriteAheadLogMode.Lazy, 3_000_000);
ZoneTree2.TestStringTree(WriteAheadLogMode.Immediate, 3_000_000);
ZoneTree2.TestStringTree(WriteAheadLogMode.Lazy, 3_000_000);
