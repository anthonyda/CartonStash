using Microsoft.Management.Deployment;
using CartonStash.Core.Models;
using CartonStash.Core.Services;

namespace CartonStash.Core.Tests;

public class WinGetErrorMessageTests
{
    [Theory] // R3.4: readable messages, never a raw HRESULT
    [InlineData(ConnectResultStatus.CatalogError, "internet connection")]
    [InlineData(ConnectResultStatus.SourceAgreementsNotAccepted, "Settings")]
    public void Connect_failures_are_readable(ConnectResultStatus status, string expected)
    {
        Assert.Contains(expected, WinGetPackageService.DescribeConnectFailure(status));
    }

    [Theory]
    [InlineData(FindPackagesResultStatus.CatalogError, "internet connection")]
    [InlineData(FindPackagesResultStatus.BlockedByPolicy, "policy")]
    [InlineData(FindPackagesResultStatus.InternalError, "InternalError")]
    public void Find_failures_are_readable(FindPackagesResultStatus status, string expected)
    {
        Assert.Contains(expected, WinGetPackageService.DescribeFindFailure(status));
    }

    [Theory] // 003 R5.2, 004 NoApplicableUpgrade
    [InlineData(InstallResultStatus.Ok, OperationResult.Succeeded, "Installed successfully")]
    [InlineData(InstallResultStatus.DownloadError, OperationResult.Failed, "download failed")]
    [InlineData(InstallResultStatus.InstallError, OperationResult.Failed, "error code 1603")]
    [InlineData(InstallResultStatus.BlockedByPolicy, OperationResult.Failed, "group policy")]
    [InlineData(InstallResultStatus.NoApplicableInstallers, OperationResult.Failed, "No installer for this PC")]
    [InlineData(InstallResultStatus.NoApplicableUpgrade, OperationResult.Failed, "No applicable update")]
    [InlineData(InstallResultStatus.PackageAgreementsNotAccepted, OperationResult.Failed, "license agreement")]
    [InlineData(InstallResultStatus.InternalError, OperationResult.Failed, "InternalError")]
    public void Install_results_are_readable(InstallResultStatus status, OperationResult result, string expected)
    {
        var outcome = WinGetPackageInstaller.MapInstallResult(OperationKind.Install, status, 1603, false, InstallScope.Any);

        Assert.Equal(result, outcome.Result);
        Assert.Contains(expected, outcome.Message);
    }

    [Theory] // 002 R6.5: uninstall the row's own program; install / upgrade by WinGet ID
    [InlineData(OperationKind.Uninstall, "MSIX-WSL-3.0.4.0", true)]
    [InlineData(OperationKind.Uninstall, null, false)]
    [InlineData(OperationKind.Upgrade, "ARP-WSL-Update", false)]
    [InlineData(OperationKind.Install, null, false)]
    public void Uninstall_targets_the_installed_entry_when_known(OperationKind kind, string? installedId, bool expected)
    {
        var package = new PackageInfo("Microsoft.WSL", "WSL", "3.0.4.0", "2.7.14", "winget") { InstalledId = installedId };

        Assert.Equal(expected, WinGetPackageInstaller.UninstallsInstalledEntry(kind, package));
    }

    [Theory] // 003 R1.2: the row's source, never the same ID from another one
    [InlineData("winget", "winget", true)]
    [InlineData("winget", "other", false)]
    [InlineData("winget", null, false)]
    [InlineData("", "other", true)]
    [InlineData(null, "other", true)]
    public void Only_the_requested_source_counts(string? requested, string? actual, bool expected)
    {
        Assert.Equal(expected, CatalogConnection.IsFromSource(requested, actual));
    }

    [Fact] // 003 R5.3
    public void Restart_required_is_a_success_that_says_so()
    {
        var outcome = WinGetPackageInstaller.MapInstallResult(OperationKind.Upgrade, InstallResultStatus.Ok, 0, true, InstallScope.Any);

        Assert.Equal(OperationResult.SucceededRestartRequired, outcome.Result);
        Assert.Equal("Updated. Restart your PC to finish.", outcome.Message);
    }

    [Fact] // 003 R5.2: seen live with JernejSimoncic.Wget (host served HTML instead of the exe)
    public void Hash_mismatch_is_not_blamed_on_the_connection()
    {
        var outcome = WinGetPackageInstaller.MapInstallResult(OperationKind.Install, InstallResultStatus.DownloadError, 0, false,
            InstallScope.Any, WinGetPackageInstaller.HashMismatch);

        Assert.Contains("checksum", outcome.Message);
        Assert.DoesNotContain("internet", outcome.Message);
    }

    [Fact] // 006: wrong scope hints at Settings
    public void No_installer_for_a_chosen_scope_suggests_changing_it()
    {
        var outcome = WinGetPackageInstaller.MapInstallResult(OperationKind.Install, InstallResultStatus.NoApplicableInstallers, 0, false, InstallScope.User);

        Assert.Contains("changing it in Settings", outcome.Message);
    }

    [Theory]
    [InlineData(UninstallResultStatus.Ok, OperationResult.Succeeded, "Uninstalled successfully")]
    [InlineData(UninstallResultStatus.UninstallError, OperationResult.Failed, "error code 0x80070005")]
    [InlineData(UninstallResultStatus.BlockedByPolicy, OperationResult.Failed, "group policy")]
    public void Uninstall_results_are_readable(UninstallResultStatus status, OperationResult result, string expected)
    {
        var outcome = WinGetPackageInstaller.MapUninstallResult(status, 0x80070005, false);

        Assert.Equal(result, outcome.Result);
        Assert.Contains(expected, outcome.Message);
    }
}
