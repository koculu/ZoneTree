namespace ZoneTree.Comparers;

public sealed class DecimalComparerAscending : IRefComparer<decimal>
{
  public int Compare(in decimal x, in decimal y)
  {
    return x.CompareTo(y);
  }
}
