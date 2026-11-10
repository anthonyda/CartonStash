using CartonStash.Core.Models;
using CartonStash.Core.Services;

namespace CartonStash.Core.Tests;

/// <summary>003 T15: WinGet's Default mode for a package Windows Installer has installed for all users (R8.1, R8.2, R8.5).</summary>
public class AllUsersMsiModeTests
{
    private static readonly PackageInfo Pandoc = new("JohnMacFarlane.Pandoc", "Pandoc", "3.12", "3.13", "winget", UpdateAvailable: true);

    [Theory] // the VM: msiexec /x /quiet failed with 1730 for an all-users MSI; /passive got Windows' prompt
    [InlineData(OperationKind.Upgrade)]
    [InlineData(OperationKind.Uninstall)]
    [InlineData(OperationKind.Install)] // going back (014) over the installed package
    public void An_all_users_MSI_runs_in_Default_mode_R8_1(OperationKind kind)
    {
        Assert.Equal(OperationMode.DefaultForAllUsersMsi, WinGetPackageInstaller.ModeFor(kind, installed: true, InstallScope.System));
    }

    [Theory]
    [InlineData(OperationKind.Upgrade, true, InstallScope.User)] // per-user MSI: no admin rights needed
    [InlineData(OperationKind.Uninstall, true, InstallScope.User)]
    [InlineData(OperationKind.Upgrade, true, null)] // not an MSI, or Windows Installer doesn't know it
    [InlineData(OperationKind.Uninstall, true, null)]
    [InlineData(OperationKind.Install, true, InstallScope.Any)]
    [InlineData(OperationKind.Install, false, InstallScope.System)] // a first install: nothing installed to remove
    [InlineData(OperationKind.Install, false, null)]
    public void Everything_else_stays_Silent_R8_2(OperationKind kind, bool installed, InstallScope? msiContext)
    {
        Assert.Equal(OperationMode.Silent, WinGetPackageInstaller.ModeFor(kind, installed, msiContext));
    }

    [Fact]
    public void The_operation_keeps_the_mode_its_run_used_R8_5()
    {
        var queue = new PackageOperationQueue(new ScriptedInstaller
        {
            AutoComplete = new OperationOutcome(OperationResult.Succeeded, "ok") { Mode = OperationMode.DefaultForAllUsersMsi },
        });

        var operation = queue.Enqueue(OperationKind.Upgrade, Pandoc);

        Assert.Equal(OperationMode.DefaultForAllUsersMsi, operation.Mode);
    }

    [Theory]
    [InlineData(OperationMode.Silent, "Mode: silent")]
    [InlineData(OperationMode.DefaultForAllUsersMsi, "Mode: default, for an all-users MSI (Windows may ask for permission)")]
    public void The_log_says_how_WinGet_ran_it_R8_5(OperationMode mode, string line)
    {
        var queue = new PackageOperationQueue(new ScriptedInstaller { AutoComplete = new OperationOutcome(OperationResult.Succeeded, "ok") { Mode = mode } });

        var operation = queue.Enqueue(OperationKind.Uninstall, Pandoc);
        var log = OperationLogStore.Compose(operation, null);

        Assert.Contains(line, log);
        Assert.True(log.IndexOf("Scope:", StringComparison.Ordinal) < log.IndexOf("Mode:", StringComparison.Ordinal)); // after 014's line
    }
}
