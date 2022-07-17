using Tenray;
using Tenray.Collections;
using ZoneTree.Segments.Disk;
using ZoneTree.WAL;

namespace ZoneTree.Core;

public delegate bool IsValueDeletedDelegate<TValue>(in TValue value);

public delegate void MarkValueDeletedDelegate<TValue>(ref TValue value);

public class ZoneTreeOptions<TKey, TValue>
{
    public int MutableSegmentMaxItemCount { get; set; } = 762600;

    public IRefComparer<TKey> Comparer { get; set; }

    public ISerializer<TKey> KeySerializer { get; set; }

    public ISerializer<TValue> ValueSerializer { get; set; }

    public IsValueDeletedDelegate<TValue> IsValueDeleted = (in TValue x) => false;

    public MarkValueDeletedDelegate<TValue> MarkValueDeleted = (ref TValue x) => { x = default; };

    public bool TryValidate(out Exception exception)
    {
        if (KeySerializer == null)
        {
            exception = new MissingOptionException("KeySerializer");
            return false;
        }
        if (ValueSerializer == null)
        {
            exception = new MissingOptionException("ValueSerializer");
            return false;
        }

        if (Comparer == null)
        {
            exception = new MissingOptionException("Comparer");
            return false;
        }

        if (RandomAccessDeviceManager == null)
        {
            exception = new MissingOptionException("RandomAccessDeviceManager");
            return false;
        }

        if (WriteAheadLogProvider == null)
        {
            exception = new MissingOptionException("WriteAheadLogProvider");
            return false;
        }
        exception = null;
        return true;
    }

    public void Validate()
    {
        if (!TryValidate(out Exception exception))
            throw exception;
    }

    public IWriteAheadLogProvider WriteAheadLogProvider { get; set; }

    public IRandomAccessDeviceManager RandomAccessDeviceManager { get; set; }
}
