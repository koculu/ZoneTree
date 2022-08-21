## ZoneTree Terminology

### Segment
The dictionary meaning of segment is to divide something into separate parts or sections.
In ZoneTree segments are individual groups of key-value pairs. Keys can be duplicated across segments.
Hence the order of segments is important. A key lookup query must start from the mutable segment first. If the key is not found there, it continues in read-only segments in FIFO order. The final lookup happens in the disk segment.


### Mutable Segment (SegmentZero)
In ZoneTree there is only one segment that can accept new key-value pairs. It is called mutable segment or SegmentZero.

ReadOnlySegment: A read-only segment is an immutable group of key-value pairs that are kept in memory. When a mutable segment is filled, it is moved to the read-only segments group and another empty mutable segment is created to accept new modifications.
There can be 0 or many read-only segments at a given time.

### Disk Segment
The disk segment is also read-only. It is an immutable group of key-value pairs that are kept in the disk.
There is only a single disk segment at a given time.

### Disk Segment Mode
There are 2 disk segment modes. 

### Single Disk Segment
 In this disk segment mode, the key-value pairs that belong to the disk segment are stored in a single file.
    
### Multi Disk Segment
In this disk segment mode, the key-value pairs that belong to the disk segment are stored in multiple files in a flat hierarchy.

### Write Ahead Log (WAL)
Write Ahead Log is a special append-only log file that persists in-memory key-value pairs into the disk to preserve the database state in case of a crash/powercut. WAL also enables the shutdown of database instances without merging the mutable segment and read-only segments into the disk.

### Write Ahead Log Mode
There are 4 types of WAL modes in ZoneTree. These WAL modes provide different levels of durability and performance.

Please see [Write Ahead Log](write-ahead-log.md) section for more details on the topic.

### MoveSegmentZeroForward
It is the atomic operation of moving the mutable segment (SegmentZero) into the read-only segments layer and creating a new and empty mutable segment.

### Merge Operation (Compaction)
ZoneTree can keep all data in memory through Mutable Segment and Readonly Segments. 

However, memory is not infinite. 

Merge operation merges the read-only segments and current disk segment into a new disk segment.

After the new disk segment is filled with data, it becomes the current disk segment of the ZoneTree.

Merge operation uses *merge K sorted array* algorithm. This is a background operation and initiated in a separate thread.

Merge operation is cancellable.

An unexpected crash of a merge operation does not harm the state of the database.