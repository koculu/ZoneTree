# Disk Segments

Disk segments are the persistent, optimized storage units produced by maintenance and merge operations.

ZoneTree's disk storage is adaptive. Key/value type shape, serializers, compression, sparse indexing, caches, and disk segment mode all affect the physical storage strategy.

## Active Disk Segment

Read-only in-memory segments are merged into an active disk segment. This reduces memory pressure and improves long-term read behavior.

## Bottom Segments

When disk segments reach configured limits or multipart merge strategies are used, older persistent data can move into bottom segments.

Bottom segments let ZoneTree manage very large datasets without requiring one enormous file.

## Adaptive Layouts

ZoneTree chooses the disk segment implementation based on key and value shape:

* fixed-size key + fixed-size value,
* fixed-size key + variable-size value,
* variable-size key + fixed-size value,
* variable-size key + variable-size value.

Small unmanaged structs can unlock simpler disk layouts because ZoneTree can store fixed-size keys and values more directly. This is another reason small immutable structs or readonly record structs can be a strong value shape when they fit the data model.

Reference types, strings, byte arrays, and variable-length serialized values use layouts with headers and offsets so the disk segment can still support efficient lookup and iteration.

## Single Vs Multipart

ZoneTree supports two disk segment modes:

* `SingleDiskSegment`
* `MultiPartDiskSegment`

`MultiPartDiskSegment` is the default. It partitions large disk segments into multiple physical parts, which keeps very large datasets more manageable and gives merge operations opportunities to carry existing parts forward instead of rewriting everything.

`SingleDiskSegment` keeps a disk segment in one physical segment. It can be simpler for smaller databases.

## Segment Size

The default high-level disk segment max item count is `20_000_000` records. With multipart disk segments, the default physical part targets are `1_500_000` to `3_000_000` records per part.

Disk segment sizing affects:

* merge cost,
* read amplification,
* file size,
* backup behavior,
* cache behavior,
* operational manageability.

Use smaller segment sizes when operational boundaries matter. Use larger segments when you want fewer files and can tolerate larger merge units.

## Sparse Indexes

Disk segments use sparse indexing to avoid loading all keys into memory. Sparse arrays provide enough structure for efficient disk search while keeping memory usage bounded.

`DefaultSparseArrayStepSize` controls the default sparse array density. A smaller step gives more index entries and can improve positioning at the cost of memory. A larger step reduces memory but may require more local search. Setting it to `0` disables default sparse array creation/loading.

The default sparse array step size is `1024`.

## Compression

Disk segments can use block-based compression to reduce storage footprint while preserving random access at the block level. Compression changes CPU, IO, and cache trade-offs.

The default disk compression profile uses LZ4 fastest compression with `4 MB` blocks.

See [compression](compression.md).

## Read Path Caches

Disk segments use caches to reduce repeated disk work. ZoneTree includes circular key/value caches and block cache behavior for disk reads.

The default circular key and value caches each hold `1024` records with a `10 second` record lifetime.

See [read-path caching](read-path-caching.md).

## Iterator Pinning

Iterators can keep disk segments alive while they scan. This protects reads from concurrent segment disposal, but long-lived iterators can delay file cleanup after merges.

Dispose iterators promptly.
