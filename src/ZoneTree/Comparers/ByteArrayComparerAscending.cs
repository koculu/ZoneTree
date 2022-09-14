namespace Tenray.ZoneTree.Comparers;

public sealed class ByteArrayComparerAscending : IRefComparer<byte[]>
{
    public int Compare(in byte[] x, in byte[] y)
    {
        var len = Math.Min(x.Length, y.Length);
        for (var i = 0; i < len; ++i)
        {
            var r = x[i] - y[i];
            if (r < 0)
                return -1;
            if (r > 0)
                return 1;
        }
        return y.Length - x.Length;
    }
}