## How fast is it?
It is possible with ZoneTree to insert 100 Million integer key-value pairs in 20 seconds using WAL mode = NONE.

Benchmark for all modes: [benchmark](https://raw.githubusercontent.com/koculu/ZoneTree/main/src/Playground/BenchmarkForAllModes.txt)

| Insert Benchmarks                               | 1M      | 2M       | 3M         | 10M        |
| ------------------------------------------------|---------|----------|------------|------------|
| int-int ZoneTree async-compressed WAL                       | 343 ms  | 506 ms   | 624 ms     | 2328 ms    |
| int-int ZoneTree sync-compressed WAL       | 885 ms  | 1821 ms  | 2737 ms    | 9250 ms    |
| int-int ZoneTree sync WAL                  | 2791 ms | 5552 ms  | 8269 ms    | 27883 ms   |
||
| str-str ZoneTree async-compressed WAL                       | 796 ms  | 1555 ms  | 2308 ms    | 8712 ms    |
| str-str ZoneTree sync-compressed WAL       | 1594 ms | 3187 ms  | 4866 ms    | 17451 ms   |
| str-str ZoneTree sync WAL                  | 3617 ms | 7083 ms  | 10481 ms   | 36714 ms   |
||
| RocksDb sync WAL                           | NOT SUPPORTED                                |
| int-int RocksDb sync-compressed WAL        | 8059 ms | 16188 ms | 23599 ms   | 61947 ms   |
| str-str RocksDb sync-compressed WAL        | 8215 ms | 16146 ms | 23760 ms   | 72491 ms   |
||

Benchmark Configuration:
```c#
DiskCompressionBlockSize = 1024 * 1024 * 10;
WALCompressionBlockSize = 1024 * 32 * 8;
DiskSegmentMode = DiskSegmentMode.SingleDiskSegment;
MutableSegmentMaxItemCount = 1_000_000;
ThresholdForMergeOperationStart = 2_000_000;
```

Additional Notes:
According to our tests, ZoneTree is stable and fast even with big data.
Tested up to 200M records in desktop computers till now.

### Environment:
```
BenchmarkDotNet=v0.13.1, OS=Windows 10.0.22000
Intel Core i7-6850K CPU 3.60GHz (Skylake), 1 CPU, 12 logical and 6 physical cores
64 GB DDR4 Memory
SSD: Samsung SSD 850 EVO 1TB
Config: 1M mutable segment size, 2M readonly segments merge-threshold
```