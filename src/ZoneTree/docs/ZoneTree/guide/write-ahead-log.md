[![Downloads](https://img.shields.io/nuget/dt/ZoneTree?style=for-the-badge&labelColor=319e12&color=55c212)](https://www.nuget.org/packages/ZoneTree/) [![ZoneTree](https://img.shields.io/github/stars/koculu/ZoneTree?style=for-the-badge&logo=github&label=github&color=f1c400&labelColor=454545&logoColor=ffffff)](https://github.com/koculu/ZoneTree)

### ZoneTree offers 4 WAL modes to let you make a flexible tradeoff.

* The sync mode provides maximum durability but slower write speed.
 In case of a crash/power cut, the sync mode ensures that the inserted data is not lost.

* The sync-compressed mode provides faster write speed but less durability.
  Compression requires chunks to be filled before appending them into the WAL file.
  It is possible to enable a periodic job to persist decompressed tail records into a separate location in a specified interval.
  See IWriteAheadLogProvider options for more details.

* The async-compressed mode provides faster write speed but less durability.
  Log entries are queued to be written in a separate thread.
  Async-compressed mode uses compression in WAL files.

* None WAL mode disables WAL completely to get maximum performance. Data still can be saved to disk by tree maintainer automatically or manually.