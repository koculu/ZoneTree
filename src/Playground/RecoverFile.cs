using System.Diagnostics;
using Tenray.ZoneTree;
using Tenray.ZoneTree.AbstractFileStream;
using Tenray.ZoneTree.Comparers;
using Tenray.ZoneTree.Core;
using Tenray.ZoneTree.Options;
using Tenray.ZoneTree.Segments.Disk;
using Tenray.ZoneTree.Serializers;
using Tenray.ZoneTree.WAL;

namespace Playground;

public class RecoverFile
{
    public static void Recover1()
    {
        var path = @"..\..\data\AsyncCompressed-50M-str-str";
        var fileStreamProvider = new LocalFileStreamProvider();
        var logger = new ConsoleLogger();
        var deviceManager = new RandomAccessDeviceManager(logger, fileStreamProvider, path);
        ZoneTreeMetaWAL<string, string>.LoadZoneTreeMetaWithoutWALRecords(deviceManager);
        var options = new ZoneTreeOptions<string, string>
        {
            Logger = logger,
            WriteAheadLogProvider = new BasicWriteAheadLogProvider(
                new ConsoleLogger(), fileStreamProvider, path),
            RandomAccessDeviceManager = deviceManager,
            DiskSegmentOptions = new()
            {
                EnableCompression = true
            },
            KeySerializer = new Utf8StringSerializer(),
            ValueSerializer = new Utf8StringSerializer(),
            Comparer = new StringOrdinalComparerAscending()
        };
        options.WriteAheadLogOptions.WriteAheadLogMode
            = WriteAheadLogMode.AsyncCompressed;

        var stopWatch = new Stopwatch();
        var disk = new DiskSegment<string, string>(54, options);
        disk.InitSparseArray(100);

        Console.WriteLine("Elapsed: " + stopWatch.ElapsedMilliseconds);
    }

    public static void Recover2()
    {
        var path = @"..\..\data\SyncCompressed-3M-transactional-int-int";
        var logger = new ConsoleLogger();
        var fileStreamProvider = new LocalFileStreamProvider();
        var deviceManager = new RandomAccessDeviceManager(logger, fileStreamProvider, path);
        var meta = ZoneTreeMetaWAL<int, int>.LoadZoneTreeMetaWithoutWALRecords(deviceManager);
        var options = new ZoneTreeOptions<int, int>
        {
            Logger = logger,
            WriteAheadLogProvider = new BasicWriteAheadLogProvider(
                new ConsoleLogger(), fileStreamProvider, path),
            WriteAheadLogOptions = meta.WriteAheadLogOptions,            
            RandomAccessDeviceManager = deviceManager,            
            DiskSegmentOptions = new()
            {
                CompressionBlockSize = meta.DiskSegmentOptions.CompressionBlockSize,
                EnableCompression = true,
            },
            KeySerializer = new Int32Serializer(),
            ValueSerializer = new Int32Serializer(),
            Comparer = new Int32ComparerAscending()
        };
        new ZoneTreeFactory<int, int>().SetOptions(options).Open();
        var stopWatch = new Stopwatch();
        //var disk = new DiskSegment<int, int>(54, options);
        //disk.InitSparseArray(100);

        Console.WriteLine("Elapsed: " + stopWatch.ElapsedMilliseconds);
    }
}