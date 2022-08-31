[![Downloads](https://img.shields.io/nuget/dt/ZoneTree?style=for-the-badge&labelColor=319e12&color=55c212)](https://www.nuget.org/packages/ZoneTree/) [![ZoneTree](https://img.shields.io/github/stars/koculu/ZoneTree?style=for-the-badge&logo=github&label=github&color=f1c400&labelColor=454545&logoColor=ffffff)](https://github.com/koculu/ZoneTree)

## Tuning Disk Segment

ZoneTree comes with 2 disk segment modes. 

#### Single Disk Segment Mode
The single disk segment mode is suitable for databases with less than ~10M records.
It performs better by avoiding handling multiple files for small databases. Above 10M record count the merge operation becomes slower.
The suggested number (10M) might change depending on the key and value sizes.

#### Multi-Part Disk Segment Mode
The multi-part disk segments mode is suitable for bigger databases. It creates multiple files for a single disk segment with randomly distributed file lengths. The random distribution of file lengths creates the opportunity of skipping the rewriting of several files.

Especially if the inserted keys are in order the merge operation becomes lightning fast for big data.

The random length distribution is controlled by 2 parameters in ZoneTree options.
[DiskSegment-MinimumRecordCount](/docs/ZoneTree/api/Tenray.ZoneTree.Options.DiskSegmentOptions.html#Tenray_ZoneTree_Options_DiskSegmentOptions_MinimumRecordCount) and [DiskSegment-MaximumRecordCount](/docs/ZoneTree/api/Tenray.ZoneTree.Options.DiskSegmentOptions.html#Tenray_ZoneTree_Options_DiskSegmentOptions_MaximumRecordCount)

The following schema describes the multi-part disk segment file distribution and how it avoids rewriting all files during merges.

```
- We have 3 files, f1, f2, f3 with random lengths between 3-5
| 1 2 5 | 7 8 9 11 | 15 16 |
- now let's merge 13 to the disk segment
| 1 2 5 | 7 8 9 11 13 | 15 16 | (only f2 is rewritten)
- now let's merge 14 to the disk segment
| 1 2 5 | 7 8 9 11 13 | 14 15 16 | (only f3 is rewritten)
```
For example, if you have a huge database with 1 billion records and you are going to merge a new key into it, it will do the merge by rewriting only a few files and will not drop most of the files.
The drop ratio can be tracked in logs using the Trace log level.

#### Compression Block Size
You can also tune your disk segment performance by adjusting the following parameters.

[DiskSegment-CompressionBlockSize](/docs/ZoneTree/api/Tenray.ZoneTree.Options.DiskSegmentOptions.html#Tenray_ZoneTree_Options_DiskSegmentOptions_CompressionBlockSize) and [DiskSegment-BlockCacheLimit](/docs/ZoneTree/api/Tenray.ZoneTree.Options.DiskSegmentOptions.html#Tenray_ZoneTree_Options_DiskSegmentOptions_BlockCacheLimit)