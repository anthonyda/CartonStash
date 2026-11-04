using CartonStash.Core.Models;

namespace CartonStash.Core.Services;

/// <summary>
/// App Installer, the package WinGet runs from (004 amendment R6). The app talks to WinGet through it, so an update of
/// App Installer only takes over once WinGet's server has stopped and started again: Windows stages the new version, and
/// WinGet still reports success.
/// </summary>
public sealed class WinGetHost
{
    private const string EntryPrefix = @"MSIX\Microsoft.DesktopAppInstaller_";
    private const string EntrySuffix = "_8wekyb3d8bbwe";

    /// <summary>
    /// App Installer's installed entry, e.g. <c>MSIX\Microsoft.DesktopAppInstaller_1.29.379.0_x64__8wekyb3d8bbwe</c>
    /// (004 T8). The only place that knows the format: a miss means today's behaviour.
    /// </summary>
    public static bool IsAppInstaller(PackageInfo package) =>
        package.InstalledId is { } id
        && id.StartsWith(EntryPrefix, StringComparison.OrdinalIgnoreCase)
        && id.EndsWith(EntrySuffix, StringComparison.OrdinalIgnoreCase);

    /// <summary>The App Installer version still in use while its update waits for a restart, this session only (R6.1, R6.4).</summary>
    public string? PendingVersion { get; private set; }

    /// <summary>The app already restarted for this update and the old version is still in use (R6.4).</summary>
    public bool StillPendingAfterRestart { get; private set; }

    public event EventHandler? Changed;

    /// <summary>Called when WinGet reports success but the old version is still installed (R6.1).</summary>
    public void SetPending(string version) => Set(version, afterRestart: false);

    /// <summary>
    /// Called after a restart for this update (R6.4), with the version the old copy left behind. If App Installer still shows
    /// it, the update keeps waiting, now for every program using WinGet to close.
    /// </summary>
    public void CheckAfterRestart(string pendingVersion, IEnumerable<PackageInfo> installed)
    {
        var appInstaller = installed.FirstOrDefault(IsAppInstaller);
        if (appInstaller?.InstalledVersion == pendingVersion)
        {
            Set(pendingVersion, afterRestart: true);
        }
        else if (PendingVersion is not null)
        {
            Set(null, afterRestart: false);
        }
    }

    /// <summary>The row is App Installer at the version that waits for a restart (R6.2).</summary>
    public bool IsPending(PackageInfo package) =>
        PendingVersion is not null && IsAppInstaller(package) && package.InstalledVersion == PendingVersion;

    private void Set(string? version, bool afterRestart)
    {
        if (PendingVersion == version && StillPendingAfterRestart == afterRestart)
        {
            return;
        }
        PendingVersion = version;
        StillPendingAfterRestart = afterRestart;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
