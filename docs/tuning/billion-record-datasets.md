# Billion-Record Datasets

ZoneTree can be used with very large datasets, but billion-record systems need deliberate layout and operations.

## Design The Keyspace

Use keys that support your dominant queries. Avoid layouts that require scanning unrelated data.

Good large-data keyspaces usually include:

* tenant or partition prefix,
* entity or index type,
* ordered field such as timestamp or sequence,
* unique suffix when needed.

## Control Memory

Do not let the mutable segment hold too many large records. Tune `MutableSegmentMaxItemCount` by record size, not only by record count.

## Keep Maintenance Healthy

Large datasets depend on steady maintenance. Monitor:

* read-only segment count,
* merge duration,
* disk growth,
* cache pressure,
* backup duration.

## Segment Strategy

Use disk segment sizing and multipart behavior to keep files operationally manageable.

## Benchmark Real Workloads

Synthetic insert rates are useful, but large systems are shaped by the full workload:

* write pattern,
* point reads,
* range scans,
* deletes,
* TTL,
* compaction,
* backup/restore,
* restart time.

## Operational Pattern

For billion-record systems, keep the storage shape observable:

* track mutable and read-only record counts,
* track bottom segment count,
* track merge duration,
* keep iterator lifetimes short,
* test restart time with realistic WAL sizes,
* rehearse backup and restore before production.

Use multiple ZoneTrees when separate tenants, partitions, time buckets, or index families need independent maintenance and backup boundaries.
