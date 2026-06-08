# Compression

ZoneTree supports compression for WAL and disk segment storage. Compression is a trade-off between CPU, disk space, and IO.

## WAL Compression

Compressed WAL modes reduce WAL size and can improve IO behavior. They also add compression/decompression cost.

Use compressed WAL when:

* WAL size matters,
* write records are compressible,
* storage bandwidth is more expensive than CPU,
* you want a balanced durability/performance mode.

## Disk Segment Compression

Disk segment compression is block-based random-access compression. It reduces persistent storage size while still allowing ZoneTree to seek and read individual compressed blocks.

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

## Test With Real Data

Compression results depend heavily on key/value shape. Benchmark with your real payloads before choosing a production setting.
