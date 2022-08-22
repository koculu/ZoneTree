using System.Runtime.InteropServices;

namespace Tenray.ZoneTree.Core;

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

    public void SlideExpiration(TimeSpan timeSpan)
    {
        Expiration = Expiration.Add(timeSpan);
    }
}
