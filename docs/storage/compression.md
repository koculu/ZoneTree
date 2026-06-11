# Compression

ZoneTree supports compression for WAL and disk segment storage. Compression is a trade-off between CPU, disk space, and IO.

## WAL Compression

Compressed WAL modes reduce WAL size and can improve IO behavior. They also add compression/decompression cost.

The default WAL mode is async compressed WAL. Its default compression profile uses LZ4 fastest compression with `256 KB` blocks.

Use compressed WAL when:

* WAL size matters,
* write records are compressible,
* storage bandwidth is more expensive than CPU,
* you want a balanced durability/performance mode.

## Disk Segment Compression

Disk segment compression is block-based random-access compression. It reduces persistent storage size while still allowing ZoneTree to seek and read individual compressed blocks.

The default disk segment compression profile uses LZ4 fastest compression with `4 MB` blocks.

It can also improve read performance when IO is the bottleneck and data compresses well.

It can hurt performance when CPU is the bottleneck or values are already compressed.

## Block Size

Compression block size affects:

* compression ratio,
* random read cost,
* cache behavior,
* merge throughput,
* memory used during compression/decompression.

Larger blocks can compress better. Smaller blocks can make random reads cheaper.

For disk segments, block size also affects block cache memory. A cached disk block is held decompressed, so larger disk compression blocks can retain more memory per cached block. The maintainer controls how long inactive decompressed blocks remain cached.

ZoneTree intentionally uses different default block sizes for WAL and disk segments. WAL blocks are smaller because they serve the write/recovery path. Disk segment blocks are larger because they serve persisted sorted data and benefit more from compression density.

## Test With Real Data

Compression results depend heavily on key/value shape. Benchmark with your real payloads before choosing a production setting.
