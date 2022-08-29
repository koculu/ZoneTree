namespace Playground.Benchmark;

public static class HumanReadableExtension
{
    public static string ToHuman(this int value)
    {
        if (value < 1_000)
            return value.ToString();
        if (value < 1_000_000)
            return (value / 1_000.0) + "K";
        if (value < 1_000_000_000)
            return (value / 1_000_000.0) + "M";
        return (value / 1_000_000_000.0) + "B";

    }
}