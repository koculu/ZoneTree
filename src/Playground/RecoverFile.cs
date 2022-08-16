using System.Diagnostics;
using Tenray.ZoneTree;
using Tenray.ZoneTree.AbstractFileStream;
using Tenray.ZoneTree.Comparers;
using Tenray.ZoneTree.Core;
using Tenray.ZoneTree.Segments.Disk;
using Tenray.ZoneTree.Serializers;
using Tenray.ZoneTree.WAL;

public class RecoverFile
{
    public static void Recover1()
    {
        var path = @"..\..\data\Lazy-50M-str-str";
        var fileStreamProvider = new LocalFileStreamProvider();
        var deviceManager = new RandomAccessDeviceManager(fileStreamProvider, path);
        var meta = ZoneTreeMetaWAL<string, string>.LoadZoneTreeMetaWithoutWALRecords(deviceManager);
        var options = new ZoneTreeOptions<string, string>
        {
            WriteAheadLogProvider = new BasicWriteAheadLogProvider(new ConsoleLogger(), fileStreamProvider, path)
            {
                WriteAheadLogMode = WriteAheadLogMode.Lazy
            },
            RandomAccessDeviceManager = deviceManager,
            EnableDiskSegmentCompression = true,
            KeySerializer = new Utf8StringSerializer(),
            ValueSerializer = new Utf8StringSerializer(),
            Comparer = new StringOrdinalComparerAscending()
        };
        var stopWatch = new Stopwatch();
        var disk = new DiskSegment<string, string>(54, options);
        disk.InitSparseArray(100);

        Console.WriteLine("Elapsed: " + stopWatch.ElapsedMilliseconds);
    }

    public static void Recover2()
    {
        var path = @"..\..\data\CompressedImmediate-3M-transactional-int-int";
        var fileStreamProvider = new LocalFileStreamProvider();
        var deviceManager = new RandomAccessDeviceManager(fileStreamProvider, path);
        var meta = ZoneTreeMetaWAL<int, int>.LoadZoneTreeMetaWithoutWALRecords(deviceManager);
        var options = new ZoneTreeOptions<int, int>
        {
            WriteAheadLogProvider = new BasicWriteAheadLogProvider(new ConsoleLogger(), fileStreamProvider, path)
            {
                WriteAheadLogMode = meta.WriteAheadLogMode
            },            
            DiskSegmentCompressionBlockSize = meta.DiskSegmentCompressionBlockSize,
            RandomAccessDeviceManager = deviceManager,
            EnableDiskSegmentCompression = true,
            KeySerializer = new Int32Serializer(),
            ValueSerializer = new Int32Serializer(),
            Comparer = new Int32ComparerAscending()
        };
        var a = new ZoneTreeFactory<int, int>().SetOptions(options).Open();
        var stopWatch = new Stopwatch();
        //var disk = new DiskSegment<int, int>(54, options);
        //disk.InitSparseArray(100);

        Console.WriteLine("Elapsed: " + stopWatch.ElapsedMilliseconds);
    }
}