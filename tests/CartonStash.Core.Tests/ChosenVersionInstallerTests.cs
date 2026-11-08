using Microsoft.Management.Deployment;
using CartonStash.Core.Models;
using CartonStash.Core.Services;
using static CartonStash.Core.Services.WinGetPackageInstaller;

namespace CartonStash.Core.Tests;

/// <summary>014 T4: what WinGet's answer becomes for a chosen version. The codes are the ones T1 measured in the VM.</summary>
public class ChosenVersionInstallerTests
{
    private const string GoBackMessage = "Git couldn't go back to 2.56.0.3. Its installer may not install over a newer version. "
        + "To go back, uninstall Git, then install 2.56.0.3. Uninstall, then install does both.";

    [Theory] // R3.3
    [InlineData(1603u, 0x8A150049u)]        // MSI: "A newer version is already installed"
    [InlineData(1u, 0x8A150006u)]           // Inno: the silent downgrade prompt answers No
    [InlineData(0x80070002u, 0x80070002u)]  // MSIX: Windows won't register the older package
    public void An_installer_error_while_going_back_says_it_may_not_go_back(uint exitCode, uint extended)
    {
        var outcome = MapInstallResult(OperationKind.Install, InstallResultStatus.InstallError, exitCode, false, InstallScope.Any,
            unchecked((int)extended), "Git", goingBackTo: "2.56.0.3");

        Assert.Equal(OperationResult.Failed, outcome.Result);
        Assert.Equal(GoBackMessage, outcome.Message);
        Assert.True(outcome.Retryable); // the cause isn't certain
    }

    [Fact] // R3.3: only while going back
    public void The_same_error_on_an_ordinary_install_keeps_its_message()
    {
        var outcome = MapInstallResult(OperationKind.Install, InstallResultStatus.InstallError, 1603, false, InstallScope.Any,
            unchecked((int)0x8A150049u), "Git");

        Assert.Equal("The installer failed with error code 1603.", outcome.Message);
    }

    [Fact] // R3.3 after 003 R7: a running program is still the reason given
    public void A_program_in_use_while_going_back_says_so()
    {
        var outcome = MapInstallResult(OperationKind.Install, InstallResultStatus.InstallError, 0, false, InstallScope.Any,
            PackageInUse, "Git", goingBackTo: "2.56.0.3");

        Assert.Equal(InUseMessage(OperationKind.Install, PackageInUse, "Git", null), outcome.Message);
    }

    [Fact] // R3.3: a download failure isn't the installer refusing
    public void A_download_error_while_going_back_is_a_download_error()
    {
        var outcome = MapInstallResult(OperationKind.Install, InstallResultStatus.DownloadError, 0, false, InstallScope.Any,
            goingBackTo: "2.56.0.3");

        Assert.StartsWith("The download failed.", outcome.Message);
    }

    [Fact] // R2.1
    public void A_version_the_source_no_longer_has_fails_without_Retry()
    {
        var outcome = VersionUnavailable("winget");

        Assert.Equal(OperationResult.Failed, outcome.Result);
        Assert.Equal("This version is no longer available from winget.", outcome.Message);
        Assert.False(outcome.Retryable);
    }

    [Fact] // R2.5: going back is checked like an update
    public void Going_back_gets_the_version_check()
    {
        var ok = new OperationOutcome(OperationResult.Succeeded, "Installed successfully.");

        Assert.Equal(CheckAfterOperation.InstalledVersion, CheckAfter(OperationKind.Install, ok, Pkg.Installed("Git.Git"), "2.56.0.3"));
        Assert.Equal(CheckAfterOperation.None, CheckAfter(OperationKind.Install, ok, Pkg.Remote("Git.Git"), "2.56.0.3")); // first install
        Assert.Equal(CheckAfterOperation.None, CheckAfter(OperationKind.Install, ok, Pkg.Installed("Git.Git"))); // no version chosen
        Assert.Equal(CheckAfterOperation.InstalledVersion, CheckAfter(OperationKind.Upgrade, ok, Pkg.Installed("Git.Git", update: true), "2.56.0.4"));
    }

    [Fact] // R2.5: WinGet still lists the version from before, with an update
    public void Going_back_that_changed_nothing_is_a_warning()
    {
        var ok = new OperationOutcome(OperationResult.Succeeded, "Installed successfully.");

        var outcome = AfterUpdate(ok, "2.56.0.4", new InstalledAfterUpdate("2.56.0.4", UpdateAvailable: true), "Git");
        var wentBack = AfterUpdate(ok, "2.56.0.4", new InstalledAfterUpdate("2.56.0.3", UpdateAvailable: true), "Git");

        Assert.Equal(OperationResult.SucceededUnconfirmed, outcome.Result);
        Assert.Same(ok, wentBack);
    }

    [Fact]
    public void Only_a_chosen_version_over_an_installed_package_goes_back()
    {
        Assert.True(GoesBack(OperationKind.Install, Pkg.Installed("A"), "0.9"));
        Assert.False(GoesBack(OperationKind.Install, Pkg.Remote("A"), "0.9"));
        Assert.False(GoesBack(OperationKind.Install, Pkg.Installed("A"), null));
        Assert.False(GoesBack(OperationKind.Upgrade, Pkg.Installed("A"), "2.0"));
    }
}
