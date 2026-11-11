using CartonStash.Core.Models;
using CartonStash.Core.Services;
using Microsoft.Management.Deployment;

namespace CartonStash.Core.Tests;

/// <summary>015 T3: updates keep the installed scope (R3.1), and say so when that overrides Settings (R3.2).</summary>
public class UpdateScopeTests
{
    [Theory] // 014's first run: WinGet read a per-user Pandoc as all users; Windows Installer knows better
    [InlineData(InstallScope.User, "Machine", InstallScope.User)]
    [InlineData(InstallScope.System, "User", InstallScope.System)]
    [InlineData(null, "User", InstallScope.User)]
    [InlineData(null, "Machine", InstallScope.System)]
    public void An_update_keeps_the_installed_scope_R3_1(InstallScope? msi, string winGet, InstallScope expected)
    {
        Assert.Equal((expected, ScopeSource.Installed), WinGetPackageInstaller.ScopeFor(null, keepsInstalled: true, msi, winGet, InstallScope.Any));
    }

    [Fact] // nothing knows the scope: today's behaviour, the setting
    public void Without_a_known_scope_an_update_uses_the_setting_R3_1()
    {
        Assert.Equal((InstallScope.Any, ScopeSource.Setting), WinGetPackageInstaller.ScopeFor(null, keepsInstalled: true, null, "Unknown", InstallScope.Any));
    }

    [Theory] // the version must have an installer for that scope; a scope-less one (portable, MSIX) fits any
    [InlineData(PackageInstallerScope.User, InstallScope.User, true)]
    [InlineData(PackageInstallerScope.System, InstallScope.System, true)]
    [InlineData(PackageInstallerScope.Unknown, InstallScope.User, true)]
    [InlineData(PackageInstallerScope.Unknown, InstallScope.System, true)]
    [InlineData(PackageInstallerScope.System, InstallScope.User, false)] // 7-Zip has no per-user installer
    [InlineData(PackageInstallerScope.User, InstallScope.System, false)]
    public void The_installer_has_to_fit_the_scope_R3_1(PackageInstallerScope picked, InstallScope scope, bool fits)
    {
        Assert.Equal(fits, WinGetPackageInstaller.InstallerFits(picked, scope));
    }

    [Fact]
    public void No_installer_doesnt_fit_R3_1()
    {
        Assert.False(WinGetPackageInstaller.InstallerFits(null, InstallScope.User));
    }

    [Theory]
    [InlineData(InstallScope.System, InstallScope.User, "Updated for the current user, the way it was installed.")]
    [InlineData(InstallScope.User, InstallScope.System, "Updated for all users, the way it was installed.")]
    public void Overriding_Settings_is_said_R3_2(InstallScope setting, InstallScope used, string message)
    {
        Assert.Equal(message, WinGetPackageInstaller.UpdatedScopeNote("Pandoc", setting, used, ScopeSource.Installed));
    }

    [Theory] // "Let WinGet decide", the same scope, or a scope that wasn't the installed one: nothing to say
    [InlineData(InstallScope.Any, InstallScope.User, ScopeSource.Installed)]
    [InlineData(InstallScope.User, InstallScope.User, ScopeSource.Installed)]
    [InlineData(InstallScope.System, InstallScope.System, ScopeSource.Installed)]
    [InlineData(InstallScope.System, InstallScope.User, ScopeSource.Setting)]
    [InlineData(InstallScope.System, InstallScope.User, ScopeSource.Chosen)]
    [InlineData(InstallScope.System, InstallScope.Any, ScopeSource.Installed)]
    public void Otherwise_nothing_is_added_R3_2(InstallScope setting, InstallScope used, ScopeSource source)
    {
        Assert.Null(WinGetPackageInstaller.UpdatedScopeNote("Pandoc", setting, used, source));
    }

    [Theory] // T9: WinGet reads a per-user Pandoc as Machine, so its upgrade with User found nothing; install instead
    [InlineData(InstallScope.User, InstallScope.System, true)]
    [InlineData(InstallScope.System, InstallScope.User, true)]
    [InlineData(InstallScope.User, InstallScope.User, false)] // they agree: WinGet's own upgrade
    [InlineData(InstallScope.System, InstallScope.System, false)]
    [InlineData(null, InstallScope.System, false)] // not an MSI Windows Installer knows
    [InlineData(InstallScope.User, null, false)] // WinGet doesn't say: its upgrade has nothing to contradict
    public void An_update_installs_when_Windows_Installer_and_WinGet_disagree_R3_1(InstallScope? msi, InstallScope? winGet, bool installs)
    {
        Assert.Equal(installs, WinGetPackageInstaller.UpdatesByInstalling(OperationKind.Upgrade, msi, winGet));
    }

    [Fact]
    public void Only_an_update_R3_1()
    {
        Assert.False(WinGetPackageInstaller.UpdatesByInstalling(OperationKind.Install, InstallScope.User, InstallScope.System));
        Assert.False(WinGetPackageInstaller.UpdatesByInstalling(OperationKind.Uninstall, InstallScope.User, InstallScope.System));
    }
}
