using System.Runtime.InteropServices;

namespace CartonStash.Core.Services;

/// <summary>The Windows region, which WinGet sends to the Store as the market (011 R2.4).</summary>
internal static class WindowsRegion
{
    /// <summary>False when the region is a wider area such as World (<c>001</c>), as in Windows Sandbox.</summary>
    public static bool IsCountry()
    {
        var name = new char[16];
        var length = GetUserDefaultGeoName(name, name.Length);
        return length <= 1 || IsCountryCode(new string(name, 0, length - 1)); // unknown: don't suggest a change
    }

    internal static bool IsCountryCode(string name) => name.Length == 2 && name.All(char.IsAsciiLetter);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetUserDefaultGeoName([Out] char[] geoName, int geoNameCount);
}
