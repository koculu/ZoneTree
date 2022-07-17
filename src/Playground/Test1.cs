using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tenray;
using ZoneTree.Serializers;

namespace Playground;

public class Test1
{
    public void Run()
    {
        var dataPath = "data/IntIntMutableSegmentOnlyAtomicIncrement";
        if (Directory.Exists(dataPath))
            Directory.Delete(dataPath, true);
        var counterKey = -3999;
        using var data = new ZoneTreeFactory<int, int>()
            .SetComparer(new Int32ComparerDescending())
            .SetDataDirectory(dataPath)
            .SetWriteAheadLogDirectory(dataPath)
            .SetKeySerializer(new Int32Serializer())
            .SetValueSerializer(new Int32Serializer())
            .OpenOrCreate();
        for (var i = 0; i < 2000; ++i)
        {
            data.Upsert(i, i + i);
        }
        var random = new Random();
        var off = -1;
        Parallel.For(0, 1001, (x) =>
        {
            try
            {
                var len = random.Next(1501);
                for (var i = 0; i < len; ++i)
                {
                    data.Upsert(i, i + i);
                }
                len = random.Next(1501);
                for (var i = 0; i < len; ++i)
                {
                    data.TryAtomicAddOrUpdate(counterKey, 0, (y) => y + 1);
                    Interlocked.Increment(ref off);
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        });

        for (var i = 0; i < 2000; ++i)
        {
            var result = data.TryGet(i, out var v);
            Assert.That(result, Is.True);
            Assert.That(v, Is.EqualTo(i + i));
            Assert.That(data.ContainsKey(i), Is.True);
        }
        data.TryGet(counterKey, out var finalValue);
        Assert.That(finalValue, Is.EqualTo(off));
        data.Maintenance.DestroyTree();
    }
}
