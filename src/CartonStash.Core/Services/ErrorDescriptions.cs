using System.Runtime.InteropServices;

namespace CartonStash.Core.Services;

/// <summary>Windows' own text for an error code, in its display language (013 R1.1).</summary>
internal static class ErrorDescriptions
{
    private const int FormatMessageFromSystem = 0x00001000;
    private const int FormatMessageIgnoreInserts = 0x00000200;

    /// <summary>
    /// The description, or null when Windows has none. WinGet's own <c>0x8A15…</c> codes have none: .NET would turn them
    /// into "Unknown error (0x…)", which is localized and says nothing, so <c>FormatMessageW</c> is called directly.
    /// </summary>
    public static string? Describe(int hresult)
    {
        var buffer = new char[512];
        var length = FormatMessageW(FormatMessageFromSystem | FormatMessageIgnoreInserts, IntPtr.Zero, hresult, 0, buffer, buffer.Length, IntPtr.Zero);
        if (length <= 0)
        {
            return null;
        }
        var text = new string(buffer, 0, length).Trim();
        return text.Length > 0 ? text : null;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int FormatMessageW(int flags, IntPtr source, int messageId, int languageId, [Out] char[] buffer, int size, IntPtr arguments);
}
