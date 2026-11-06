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

    [Theory] // 003 R7.1
    [InlineData(OperationKind.Install, 0x8A150101u, "Claude Code is running, so it couldn't be installed. Close it, then press Retry.")]
    [InlineData(OperationKind.Upgrade, 0x8A150103u, "Claude Code is running, so it couldn't be updated. Close it, then press Retry.")]
    [InlineData(OperationKind.Upgrade, 0x8A150111u,
        "Claude Code is in use by another program, so it couldn't be updated. Close that program, then press Retry.")]
    public void In_use_codes_ask_to_close_the_program(OperationKind kind, uint code, string expected)
    {
        var outcome = WinGetPackageInstaller.MapInstallResult(kind, InstallResultStatus.InstallError, 2316632067, false, InstallScope.Any,
            unchecked((int)code), "Claude Code");

        Assert.Equal(OperationResult.Failed, outcome.Result);
        Assert.Equal(expected, outcome.Message);
    }

    [Theory] // 003 R7.1
    [InlineData(0x8A150101u, "Claude Code is running, so it couldn't be uninstalled. Close it, then press Retry.")]
    [InlineData(0x8A150111u, "Claude Code is in use by another program, so it couldn't be uninstalled. Close that program, then press Retry.")]
    public void In_use_codes_ask_to_close_the_program_on_uninstall(uint code, string expected)
    {
        var outcome = WinGetPackageInstaller.MapUninstallResult(UninstallResultStatus.UninstallError, 1, false, unchecked((int)code), "Claude Code");

        Assert.Equal(expected, outcome.Message);
    }

    [Fact] // 003 R7.2: the update seen on 2026-11-06, with claude.exe running
    public void Portable_update_names_the_running_file()
    {
        var outcome = WinGetPackageInstaller.MapInstallResult(OperationKind.Upgrade, InstallResultStatus.InstallError, 2316632067, false,
            InstallScope.Any, WinGetPackageInstaller.PortableInstallFailed, "Claude Code", ["claude.exe"]);

        Assert.Equal(OperationResult.Failed, outcome.Result);
        Assert.Equal("Claude Code is running (claude.exe), so it couldn't be updated. Close it, then press Retry.", outcome.Message);
    }

    [Fact] // 003 R7.2
    public void Portable_uninstall_names_every_running_file()
    {
        var outcome = WinGetPackageInstaller.MapUninstallResult(UninstallResultStatus.UninstallError, 1, false,
            WinGetPackageInstaller.PortableUninstallFailed, "Tools", ["a.exe", "b.exe"]);

        Assert.Equal("Tools is running (a.exe, b.exe), so it couldn't be uninstalled. Close it, then press Retry.", outcome.Message);
    }

    [Theory] // 003 R7.3: nothing found, so today's message
    [InlineData(false)]
    [InlineData(true)]
    public void Portable_failure_without_a_running_file_keeps_the_error_code(bool emptyList)
    {
        var install = WinGetPackageInstaller.MapInstallResult(OperationKind.Upgrade, InstallResultStatus.InstallError, 2316632067, false,
            InstallScope.Any, WinGetPackageInstaller.PortableInstallFailed, "Claude Code", emptyList ? [] : null);
        var uninstall = WinGetPackageInstaller.MapUninstallResult(UninstallResultStatus.UninstallError, 1, false,
            WinGetPackageInstaller.PortableUninstallFailed, "Claude Code", emptyList ? [] : null);

        Assert.Equal("The installer failed with error code 0x8A150003.", install.Message);
        Assert.Equal("The uninstaller failed with error code 1.", uninstall.Message);
    }

    [Fact] // 003 R7.3: running files only count after a portable failure
    public void Running_files_alone_change_nothing()
    {
        var outcome = WinGetPackageInstaller.MapInstallResult(OperationKind.Upgrade, InstallResultStatus.InstallError, 1603, false,
            InstallScope.Any, 0, "Claude Code", ["claude.exe"]);

        Assert.Equal("The installer failed with error code 1603.", outcome.Message);
    }

    [Fact] // 003 R7.1: a name is always shown, even when the row has none
    public void In_use_message_without_a_name_says_the_package()
    {
        Assert.StartsWith("The package is running",
            WinGetPackageInstaller.InUseMessage(OperationKind.Upgrade, WinGetPackageInstaller.PackageInUse, "", null));
    }

    [Fact] // 003 R7.5: the log keeps WinGet's codes as they are (the Claude Code log of 2026-11-06)
    public void Details_are_unchanged_for_an_in_use_failure()
    {
        var details = WinGetPackageInstaller.FormatDetails("InstallError", 2316632067,
            new System.Runtime.InteropServices.COMException("", WinGetPackageInstaller.PortableInstallFailed));

        Assert.Equal("WinGet status: InstallError\nInstaller exit code: 2316632067\nExtended error: 0x8A150052", details);
    }

    private sealed class FakeRunningPrograms(Func<string, IReadOnlyList<string>> find) : IRunningPrograms
    {
        public List<string> Asked { get; } = [];

        public IReadOnlyList<string> FileNamesIn(string folder)
        {
            Asked.Add(folder);
            return find(folder);
        }
    }

    private const string ClaudeFolder = @"C:\Users\t\AppData\Local\Microsoft\WinGet\Packages\Anthropic.ClaudeCode_Microsoft.Winget.Source_8wekyb3d8bbwe";

    [Theory] // 003 R7.2: both portable codes look in the install folder
    [InlineData(0x8A150052u)]
    [InlineData(0x8A150057u)]
    public void Running_files_come_from_the_install_folder(uint code)
    {
        var programs = new FakeRunningPrograms(_ => ["claude.exe"]);

        var files = WinGetPackageInstaller.RunningFilesAfter(unchecked((int)code), () => ClaudeFolder, programs);

        Assert.Equal(["claude.exe"], files);
        Assert.Equal([ClaudeFolder], programs.Asked);
    }

    [Theory] // 003 R7.2: any other result reads nothing
    [InlineData(0u)]
    [InlineData(0x8A150003u)]
    [InlineData(0x8A150101u)]
    public void Running_files_fallback_reads_nothing_after_other_codes(uint code)
    {
        var programs = new FakeRunningPrograms(_ => ["claude.exe"]);
        var folderRead = false;

        var files = WinGetPackageInstaller.RunningFilesAfter(unchecked((int)code), () => { folderRead = true; return ClaudeFolder; }, programs);

        Assert.Null(files);
        Assert.False(folderRead);
        Assert.Empty(programs.Asked);
    }

    [Theory] // 003 R7.3: no folder
    [InlineData(null)]
    [InlineData("")]
    public void Running_files_fallback_without_a_folder(string? folder)
    {
        var programs = new FakeRunningPrograms(_ => ["claude.exe"]);

        Assert.Null(WinGetPackageInstaller.RunningFilesAfter(WinGetPackageInstaller.PortableInstallFailed, () => folder, programs));
        Assert.Empty(programs.Asked);
    }

    [Fact] // 003 R7.3: a COM error reading the folder, or the process list throwing, gives today's message
    public void Running_files_fallback_when_a_read_throws()
    {
        var throwing = new FakeRunningPrograms(_ => throw new InvalidOperationException("process list"));

        Assert.Null(WinGetPackageInstaller.RunningFilesAfter(WinGetPackageInstaller.PortableInstallFailed,
            () => throw new System.Runtime.InteropServices.COMException("gone"), new FakeRunningPrograms(_ => ["claude.exe"])));
        Assert.Null(WinGetPackageInstaller.RunningFilesAfter(WinGetPackageInstaller.PortableInstallFailed, () => ClaudeFolder, throwing));

        var outcome = WinGetPackageInstaller.MapInstallResult(OperationKind.Upgrade, InstallResultStatus.InstallError, 2316632067, false,
            InstallScope.Any, WinGetPackageInstaller.PortableInstallFailed, "Claude Code",
            WinGetPackageInstaller.RunningFilesAfter(WinGetPackageInstaller.PortableInstallFailed, () => ClaudeFolder, throwing));
        Assert.Equal("The installer failed with error code 0x8A150003.", outcome.Message);
    }

    [Fact] // 003 R7.3: nothing running from the folder
    public void Running_files_fallback_when_nothing_runs()
    {
        var files = WinGetPackageInstaller.RunningFilesAfter(WinGetPackageInstaller.PortableInstallFailed, () => ClaudeFolder,
            new FakeRunningPrograms(_ => []));

        Assert.NotNull(files);
        Assert.Empty(files);
    }

    [Theory] // 004 R6.1: WinGet's Ok, then its installed list read again
    [InlineData(true, OperationResult.SucceededAppRestartRequired, "Restart CartonStash to finish")]
    [InlineData(false, OperationResult.Succeeded, "Updated successfully.")]
    [InlineData(null, OperationResult.Succeeded, "Updated successfully.")]
    public void App_Installer_update_waits_for_a_restart_while_the_old_version_is_installed(bool? stillInstalled, OperationResult result, string message)
    {
        var ok = WinGetPackageInstaller.MapInstallResult(OperationKind.Upgrade, InstallResultStatus.Ok, 0, false, InstallScope.Any) with { Details = "WinGet status: Ok" };

        var outcome = WinGetPackageInstaller.AfterAppInstallerUpdate(ok, stillInstalled);

        Assert.Equal(result, outcome.Result);
        Assert.Contains(message, outcome.Message);
        Assert.DoesNotContain(stillInstalled == true ? "successfully" : "Restart CartonStash", outcome.Message);
        Assert.Equal("WinGet status: Ok", outcome.Details);
    }
}
