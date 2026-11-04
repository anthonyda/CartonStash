using CartonStash.Core.Models;
using CartonStash.Core.Services;

namespace CartonStash.Core.Tests;

public class WinGetHostTests
{
    internal static PackageInfo AppInstaller(string version = "1.29.379.0") =>
        new PackageInfo("Microsoft.AppInstaller", "App Installer", version, "1.29.380", "winget", true)
        {
            InstalledId = $@"MSIX\Microsoft.DesktopAppInstaller_{version}_x64__8wekyb3d8bbwe",
        };

    [Theory] // 004 T8: the entry as WinGet reports it
    [InlineData(@"MSIX\Microsoft.DesktopAppInstaller_1.29.379.0_x64__8wekyb3d8bbwe", true)]
    [InlineData(@"MSIX\Microsoft.DesktopAppInstaller_1.29.380.0_arm64__8wekyb3d8bbwe", true)]
    [InlineData(@"MSIX\Microsoft.WindowsTerminal_1.23.1.0_x64__8wekyb3d8bbwe", false)]
    [InlineData(@"MSIX\Microsoft.DesktopAppInstallerPreview_1.0.0.0_x64__8wekyb3d8bbwe", false)]
    [InlineData(@"MSIX\Microsoft.DesktopAppInstaller_1.29.379.0_x64__otherpublisher", false)]
    [InlineData(@"ARP\Machine\X64\Microsoft.DesktopAppInstaller_1.0", false)]
    public void Recognises_App_Installer_by_its_installed_entry(string installedId, bool expected) // R6.1, R6.2
    {
        var package = new PackageInfo("Microsoft.AppInstaller", "App Installer", "1.0", "2.0", "winget", true) { InstalledId = installedId };

        Assert.Equal(expected, WinGetHost.IsAppInstaller(package));
    }

    [Fact]
    public void A_search_result_is_never_App_Installer() =>
        Assert.False(WinGetHost.IsAppInstaller(Pkg.Remote("Microsoft.AppInstaller")));

    [Fact]
    public void Pending_applies_to_App_Installer_at_that_version_only() // R6.2
    {
        var host = new WinGetHost();
        var changes = 0;
        host.Changed += (_, _) => changes++;

        host.SetPending("1.29.379.0");

        Assert.Equal(1, changes);
        Assert.True(host.IsPending(AppInstaller()));
        Assert.False(host.IsPending(AppInstaller("1.29.380.0")));
        Assert.False(host.IsPending(Pkg.Installed("Microsoft.WindowsTerminal", update: true)));
        Assert.False(host.StillPendingAfterRestart);
    }

    [Fact]
    public void After_a_restart_the_new_version_clears_it() // R6.4
    {
        var host = new WinGetHost();

        host.CheckAfterRestart("1.29.379.0", [AppInstaller("1.29.380.0")]);

        Assert.Null(host.PendingVersion);
        Assert.False(host.StillPendingAfterRestart);
    }

    [Fact]
    public void After_a_restart_the_old_version_keeps_it_waiting() // R6.4
    {
        var host = new WinGetHost();

        host.CheckAfterRestart("1.29.379.0", [Pkg.Installed("Git.Git"), AppInstaller()]);

        Assert.Equal("1.29.379.0", host.PendingVersion);
        Assert.True(host.StillPendingAfterRestart);
        Assert.True(host.IsPending(AppInstaller()));
    }
}
