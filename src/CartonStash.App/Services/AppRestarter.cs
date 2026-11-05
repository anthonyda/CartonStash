using System.Diagnostics;
using Microsoft.Windows.AppLifecycle;
using CartonStash.Core.Services;

namespace CartonStash.App.Services;

/// <summary>
/// Restarts the app to finish App Installer's update (004 R6.3). <c>AppInstance.Restart</c> ends this copy cleanly: WinGet's
/// old server then stops as after a normal close, about 10 s later (T8), and the new copy waits for it.
/// </summary>
public sealed class AppRestarter : IAppRestarter
{
    private const string ServerProcessName = "WindowsPackageManagerServer";

    /// <summary>The page to open again; set once the main window exists.</summary>
    public Func<string?> CurrentPage { get; set; } = () => null;

    public bool Restart(string? pendingVersion)
    {
        var args = new RestartArguments(Environment.ProcessId, ServerProcessIds(), pendingVersion, CurrentPage()).Format();
        AppInstance.Restart(args); // returns only if it failed
        return false;
    }

    /// <summary>The new copy waits for these, at most <paramref name="limit"/> in all (R6.3, R6.4).</summary>
    public static void WaitForExit(IEnumerable<int> processIds, TimeSpan limit)
    {
        var end = DateTime.UtcNow + limit;
        foreach (var id in processIds)
        {
            try
            {
                using var process = Process.GetProcessById(id);
                var left = end - DateTime.UtcNow;
                if (left > TimeSpan.Zero)
                {
                    process.WaitForExit(left);
                }
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                // Already gone.
            }
        }
    }

    // Only this Windows session's: another user's WinGet doesn't hold ours.
    private static List<int> ServerProcessIds()
    {
        var session = Process.GetCurrentProcess().SessionId;
        var ids = new List<int>();
        foreach (var process in Process.GetProcessesByName(ServerProcessName))
        {
            using (process)
            {
                if (process.SessionId == session)
                {
                    ids.Add(process.Id);
                }
            }
        }
        return ids;
    }
}
