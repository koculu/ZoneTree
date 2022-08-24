using System.Diagnostics;
using System.Reflection;

namespace Tenray.ZoneTree.Core;

public static class ZoneTreeInfo
{
    static Version Version = null;

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
            var str = FileVersionInfo
                .GetVersionInfo(Assembly.GetExecutingAssembly().Location)
                .FileVersion;
            Version = Version.Parse(str);
            return Version;
        }
    }
}
