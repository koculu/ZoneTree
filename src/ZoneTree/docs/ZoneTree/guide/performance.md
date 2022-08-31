[![Downloads](https://img.shields.io/nuget/dt/ZoneTree?style=for-the-badge&labelColor=319e12&color=55c212)](https://www.nuget.org/packages/ZoneTree/) [![ZoneTree](https://img.shields.io/github/stars/koculu/ZoneTree?style=for-the-badge&logo=github&label=github&color=f1c400&labelColor=454545&logoColor=ffffff)](https://github.com/koculu/ZoneTree)

## How fast is it?
It is possible with ZoneTree to insert 100 Million integer key-value pairs in 20 seconds using WAL mode = NONE.

Benchmark for all modes: [benchmark](https://raw.githubusercontent.com/koculu/ZoneTree/main/src/Playground/BenchmarkForAllModes.txt)

| Insert Benchmarks                               | 1M      | 2M       | 3M         | 10M        |
| ------------------------------------------------|---------|----------|------------|------------|
| int-int ZoneTree async-compressed WAL                       | 267 ms  | 464 ms   | 716 ms     | 2693 ms    |
| int-int ZoneTree sync-compressed WAL       | 834 ms  | 1617 ms  | 2546 ms    | 8642 ms    |
| int-int ZoneTree sync WAL                  | 2742 ms | 5533 ms  | 8242 ms    | 27497 ms   |
||
| str-str ZoneTree async-compressed WAL                       | 892 ms  | 1833 ms  | 2711 ms    | 9443 ms    |
| str-str ZoneTree sync-compressed WAL       | 1752 ms | 3397 ms  | 5070 ms    | 19153 ms   |
| str-str ZoneTree sync WAL                  | 3488 ms | 7002 ms  | 10483 ms   | 38727 ms   |
||
| RocksDb sync WAL (10K => 11 sec)           | ~1.100.000 ms | N/A | N/A | N/A                             |
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
Tested up to 1 billion records in desktop computers till now.

### Environment:
```
BenchmarkDotNet=v0.13.1, OS=Windows 10.0.22000
Intel Core i7-6850K CPU 3.60GHz (Skylake), 1 CPU, 12 logical and 6 physical cores
64 GB DDR4 Memory
SSD: Samsung SSD 850 EVO 1TB
Config: 1M mutable segment size, 2M readonly segments merge-threshold
```