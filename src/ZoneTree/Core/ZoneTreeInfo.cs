using System.Diagnostics;
using System.Reflection;

namespace Tenray.ZoneTree.Core;

public static class ZoneTreeInfo
{
    static Version Version;

    /// <summary>
    /// Gets ZoneTree Product Version
    /// </summary>
    /// <returns></returns>
    public static Version ProductVersion
    {
        get
        {
            if (Version != null)
                return Version;
            Version = Assembly.GetExecutingAssembly().GetName().Version;
            return Version;
        }
    }
}
