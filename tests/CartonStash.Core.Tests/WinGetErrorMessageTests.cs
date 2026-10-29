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
