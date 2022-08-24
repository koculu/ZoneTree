namespace Tenray.ZoneTree.Comparers;


/// <summary>
/// Defines a method that a type implements to compare two objects.
/// </summary>
/// <typeparam name="TKey">Key type</typeparam>
public interface IRefComparer<TKey>
{
    /// <summary>
    /// Compares two objects and returns a value indicating
    /// whether one is less than, equal to, or greater than the other.
    /// </summary>
    /// <param name="x">The first key</param>
    /// <param name="y">The second key</param>
    /// <returns>-1 (x &lt; y), 0 (x == y) or 1 (x &gt; y)</returns>    
    int Compare(in TKey x, in TKey y);
}
