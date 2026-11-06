using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace CartonStash.Core.Services;

/// <summary>
/// <see cref="IRunningPrograms"/> on the process list. Paths come from <c>QueryFullProcessImageNameW</c> with
/// <c>PROCESS_QUERY_LIMITED_INFORMATION</c>, which opens the user's elevated processes too and gives no right to end
/// them (003 R7.4). <see cref="Process.MainModule"/> needs more rights and throws for those (design, R7).
/// </summary>
public sealed class RunningPrograms : IRunningPrograms
{
    private const uint ProcessQueryLimitedInformation = 0x1000;

    public IReadOnlyList<string> FileNamesIn(string folder)
    {
        var names = new List<string>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                if (ImagePath(process.Id) is { } path && IsIn(path, folder)
                    && !names.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
                {
                    names.Add(Path.GetFileName(path));
                }
            }
        }
        return names;
    }

    /// <summary>True when <paramref name="path"/> is in <paramref name="folder"/> or below it: <c>...\Foo</c> doesn't hold <c>...\FooBar\x.exe</c>.</summary>
    internal static bool IsIn(string path, string folder)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(folder))
        {
            return false;
        }
        try
        {
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)) + Path.DirectorySeparatorChar;
            return Path.GetFullPath(path).StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    /// <summary>The process's executable path, or null when it can't be opened (protected, another user's, or gone).</summary>
    private static string? ImagePath(int processId)
    {
        var handle = OpenProcess(ProcessQueryLimitedInformation, false, (uint)processId);
        if (handle == IntPtr.Zero)
        {
            return null;
        }
        try
        {
            var buffer = new StringBuilder(32768);
            var size = (uint)buffer.Capacity;
            return QueryFullProcessImageNameW(handle, 0, buffer, ref size) ? buffer.ToString(0, (int)size) : null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, uint processId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageNameW(IntPtr process, uint flags, StringBuilder exeName, ref uint size);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);
}
