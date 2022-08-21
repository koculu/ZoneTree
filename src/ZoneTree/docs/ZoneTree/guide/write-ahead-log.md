### ZoneTree offers 4 WAL modes to let you make a flexible tradeoff.

* The Immediate mode provides maximum durability but slower write speed.
 In case of a crash/power cut, the immediate mode ensures that the inserted data is not lost. RocksDb does not have immediate WAL mode. It has a WAL mode similar to the CompressedImmediate mode.

[reference](http://rocksdb.org/blog/2017/08/25/flushwal.html)

* The CompressedImmediate mode provides faster write speed but less durability.
  Compression requires chunks to be filled before appending them into the WAL file.
  It is possible to enable a periodic job to persist decompressed tail records into a separate location in a specified interval.
  See IWriteAheadLogProvider options for more details.

* The Lazy mode provides faster write speed but less durability.
  Log entries are queued to be written in a separate thread.
  Lazy mode uses compression in WAL files and provides immediate tail record persistence.

* None WAL mode disables WAL completely to get maximum performance. Data still can be saved to disk by tree maintainer automatically or manually.