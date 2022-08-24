using System.Runtime.InteropServices;

namespace Tenray.ZoneTree.PresetTypes;

[StructLayout(LayoutKind.Sequential)]
public struct TTLValue<TValue>
{
    public TValue Value;

    public DateTime Expiration;

    public TTLValue(in TValue value, DateTime expiration)
    {
        Value = value;
        Expiration = expiration;
    }

    public bool IsExpired => DateTime.UtcNow >= Expiration;

    public void Expire()
    {
        Expiration = new DateTime();
    }

    public bool SlideExpiration(TimeSpan timeSpan)
    {
        var newExpiration = DateTime.UtcNow.Add(timeSpan);
        if (newExpiration <= Expiration)
            return false;
        Expiration = newExpiration;
        return true;
    }
}
