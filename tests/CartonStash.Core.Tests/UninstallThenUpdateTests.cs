using CartonStash.Core.Models;
using CartonStash.Core.Services;

namespace CartonStash.Core.Tests;

/// <summary>020: Uninstall, then update (R1 to R4) and Install over it (R5), from a failed update's card and row.</summary>
public class UninstallThenUpdateTests
{
    private static readonly PackageInfo Miniconda = new("Anaconda.Miniconda3", "Miniconda3", "py312_24.11.1-0", "py314_26.9.4-1", "winget", true)
    {
        InstalledId = @"ARP\User\X64\Miniconda3 py312_24.11.1-0 (Python 3.12.8 64-bit)",
    };

    private static readonly PackageInfo Ea = new("ElectronicArts.EADesktop", "EA app", "13.813.1.6327", "13.832.0.6345", "winget", true);

    /// <summary>013 R3.1's result, as the installer maps it now (T3), with the installed scope it asked for.</summary>
    private static readonly OperationOutcome Refused = new(OperationResult.Failed, "Miniconda3 can't be updated in place.")
    {
        Retryable = false,
        OffersUninstallFirst = true,
        Scope = InstallScope.User,
        ScopeSource = ScopeSource.Installed,
    };

    private static readonly OperationOutcome NoUninstaller = new(OperationResult.Failed, "EA app's update couldn't start.")
    {
        OffersInstallOver = true,
        Scope = InstallScope.System,
        ScopeSource = ScopeSource.Installed,
    };

    private static (TestApp App, PackageOperation Failed) FailedUpdate(PackageInfo package, OperationOutcome outcome, OperationOptions? options = null)
    {
        var app = new TestApp();
        var failed = app.Queue.Enqueue(OperationKind.Upgrade, package, options: options);
        app.Installer.Complete(package.Id, outcome);
        return (app, failed);
    }

    // --- R1

    [Fact] // R1.1: in place of Retry
    public void A_refused_update_offers_it()
    {
        var (_, failed) = FailedUpdate(Miniconda, Refused);

        Assert.True(failed.CanUninstallThenUpdate);
        Assert.False(failed.CanRetry);
        Assert.Equal("Uninstall, then update", failed.UninstallThenUpdateText);
        Assert.Equal("Uninstall Miniconda3, then update to py314_26.9.4-1", failed.UninstallThenUpdateName); // R4.3
    }

    [Fact] // R1.1: only for an update the installer refused in place
    public void It_is_not_offered_for_other_results()
    {
        var app = new TestApp();
        var other = app.Queue.Enqueue(OperationKind.Upgrade, Pkg.Installed("A", update: true));
        app.Installer.Complete("A", OperationResult.Failed, "The installer failed with error code 1603.");
        var install = app.Queue.Enqueue(OperationKind.Install, Pkg.Remote("B"));
        app.Installer.Complete("B", Refused);

        Assert.False(other.CanUninstallThenUpdate);
        Assert.False(install.CanUninstallThenUpdate);
        Assert.False(install.CanUninstallThenInstall);
    }

    [Fact] // R1.1: the row shows it in place of Update
    public void The_row_shows_it_in_place_of_update()
    {
        var (app, failed) = FailedUpdate(Miniconda, Refused);
        var row = new CartonStash.Core.ViewModels.PackageRowViewModel(Miniconda, app.Actions, CartonStash.Core.ViewModels.RowActions.All) { Operation = failed };

        Assert.True(row.ShowUninstallThenUpdate);
        Assert.False(row.ShowUpdate);
        Assert.Equal("Uninstall Miniconda3, then update to py314_26.9.4-1", row.UninstallThenUpdateName);
    }

    // --- R2

    [Fact] // R2.1
    public void It_asks_first_naming_both_versions_and_the_scope()
    {
        var (app, failed) = FailedUpdate(Miniconda, Refused);
        app.Dialogs.Answer = false;

        failed.UninstallThenUpdateCommand.Execute(null);

        Assert.Equal("Uninstall Miniconda3, then update to py314_26.9.4-1? | Miniconda3 py312_24.11.1-0 will be uninstalled, then "
            + "py314_26.9.4-1 installed for the current user, as it is now. Settings and data that Miniconda3 keeps may be lost. "
            + "Apps that come as an MSIX package always lose them.", Assert.Single(app.Dialogs.Confirmations));
        Assert.Single(app.Installer.Calls); // P2: Cancel queues nothing
    }

    [Fact] // R2.2, R2.3, R2.5, R3.3: uninstall, wait until gone, then install the target for the current user
    public void Confirming_uninstalls_waits_then_installs_the_target_as_an_update()
    {
        var (app, failed) = FailedUpdate(Miniconda, Refused);

        failed.UninstallThenUpdateCommand.Execute(null);
        var uninstall = app.Queue.Find(Miniconda)!;
        Assert.Equal(OperationKind.Uninstall, uninstall.Kind);
        Assert.Equal("Then installs py314_26.9.4-1 for the current user.", uninstall.ThenText);
        Assert.True(uninstall.ShowThenText);
        Assert.Equal(2, app.Installer.Calls.Count); // no install yet

        app.Installer.Complete(Miniconda.Id);

        Assert.Equal(["Anaconda.Miniconda3"], app.Installer.WaitedFor); // R2.5
        Assert.False(uninstall.ShowThenText);
        var install = app.Queue.Find(Miniconda)!;
        Assert.Equal(OperationKind.Install, app.Installer.Calls[^1].Kind);
        Assert.Equal("py314_26.9.4-1", app.Installer.Versions[^1]);
        Assert.Equal(InstallScope.User, app.Installer.Scopes[^1]); // R2.3
        Assert.Equal("Update", install.KindText);

        app.Installer.Complete(Miniconda.Id, OperationResult.Succeeded, "Installed successfully.");

        Assert.Equal("Updated to py314_26.9.4-1.", install.Message); // R3.3
        Assert.Equal("Updated", install.StatusText);
    }

    [Fact] // R2.4: never elevated, even after an elevated update
    public void The_chain_runs_with_the_users_rights()
    {
        var (app, failed) = FailedUpdate(Miniconda, Refused, new OperationOptions { Elevated = true });

        failed.UninstallThenUpdateCommand.Execute(null);

        Assert.False(app.Queue.Find(Miniconda)!.Options.Elevated);
    }

    [Fact] // R2.5: still listed after the wait: nothing installed
    public void A_package_still_listed_after_the_wait_is_not_installed()
    {
        var (app, failed) = FailedUpdate(Miniconda, Refused);
        app.Installer.UninstalledInTime = false;
        failed.UninstallThenUpdateCommand.Execute(null);

        app.Installer.Complete(Miniconda.Id);

        var uninstall = app.Queue.Find(Miniconda)!;
        Assert.Equal(OperationKind.Uninstall, app.Installer.Calls[^1].Kind);
        Assert.Equal(OperationResult.Failed, uninstall.Result);
        Assert.Equal("The uninstaller finished, but Windows still lists Miniconda3 5 minutes later. "
            + "Miniconda3 py312_24.11.1-0 is still installed; nothing was updated.", uninstall.Message);
    }

    [Fact] // R2.5 for 014's chain: it names the version instead
    public void Going_back_still_listed_names_the_version()
    {
        var operation = new PackageOperation(OperationKind.Uninstall, Miniconda, false, _ => { }, _ => { }, _ => { }, () => { },
            new OperationOptions { ThenInstall = "py312_24.9.2-0" });

        var outcome = PackageOperationQueue.StillListedOutcome(operation, new(OperationResult.Succeeded, "Uninstalled successfully."), "py312_24.9.2-0");

        Assert.Equal("The uninstaller finished, but Windows still lists Miniconda3 5 minutes later. py312_24.9.2-0 wasn't installed.", outcome.Message);
    }

    // --- R3

    [Theory] // R3.1
    [InlineData(OperationResult.Failed)]
    [InlineData(OperationResult.Cancelled)]
    public void A_failed_uninstall_installs_nothing_and_says_the_old_version_stays(OperationResult result)
    {
        var (app, failed) = FailedUpdate(Miniconda, Refused);
        failed.UninstallThenUpdateCommand.Execute(null);

        app.Installer.Complete(Miniconda.Id, result, "The uninstaller failed with error code 5.");

        Assert.Equal(OperationKind.Uninstall, app.Installer.Calls[^1].Kind);
        Assert.Empty(app.Installer.WaitedFor);
        Assert.Equal("The uninstaller failed with error code 5. Miniconda3 py312_24.11.1-0 is still installed; nothing was updated.",
            app.Queue.Find(Miniconda)!.Message);
    }

    [Fact] // R3.2
    public void A_failed_install_after_the_uninstall_offers_the_previous_version()
    {
        var (app, failed) = FailedUpdate(Miniconda, Refused);
        failed.UninstallThenUpdateCommand.Execute(null);
        app.Installer.Complete(Miniconda.Id);

        app.Installer.Complete(Miniconda.Id, OperationResult.Failed, "The installer failed with error code 2.");

        var install = app.Queue.Find(Miniconda)!;
        Assert.Equal("Miniconda3 is no longer installed: it was uninstalled to make way for py314_26.9.4-1, and then that install "
            + "didn't finish. The installer failed with error code 2.", install.Message);
        Assert.True(install.CanInstallPrevious);
        Assert.Equal("Install py312_24.11.1-0", install.InstallPreviousText);
        Assert.False(install.CanUninstallThenUpdate);
    }

    // --- R4.1

    [Fact] // R4.1: a refused update queues nothing by itself
    public void A_refused_update_does_nothing_on_its_own()
    {
        var (app, _) = FailedUpdate(Miniconda, Refused);
        var (app2, _) = FailedUpdate(Ea, NoUninstaller);

        Assert.Single(app.Installer.Calls);
        Assert.Single(app2.Installer.Calls);
        Assert.Empty(app.Dialogs.Confirmations);
    }

    // --- R5

    [Fact] // R5.1
    public void An_update_without_its_uninstaller_offers_install_over_it_next_to_retry()
    {
        var (app, failed) = FailedUpdate(Ea, NoUninstaller);
        var row = new CartonStash.Core.ViewModels.PackageRowViewModel(Ea, app.Actions, CartonStash.Core.ViewModels.RowActions.All) { Operation = failed };

        Assert.True(failed.CanInstallOver);
        Assert.True(failed.CanRetry);
        Assert.Equal("Install EA app 13.832.0.6345 over the installed one", failed.InstallOverName);
        Assert.True(row.ShowInstallOver);
        Assert.True(row.ShowUpdate);
    }

    [Fact] // R5.3
    public void Install_over_it_asks_then_runs_the_update_as_an_install()
    {
        var (app, failed) = FailedUpdate(Ea, NoUninstaller);

        failed.InstallOverCommand.Execute(null);

        Assert.Equal("Install EA app 13.832.0.6345 over the installed one? | 13.832.0.6345 will be installed for all users, as EA app "
            + "is now. Nothing is uninstalled first: the installed version's uninstaller is missing. If EA app is running, close it first.",
            Assert.Single(app.Dialogs.Confirmations));
        Assert.Equal(OperationKind.Upgrade, app.Installer.Calls[^1].Kind);
        Assert.True(app.Installer.InstallOvers[^1]);
        Assert.Equal("13.832.0.6345", app.Installer.Versions[^1]);
    }

    [Fact] // R5.3: Cancel queues nothing; a failed install over it offers only Retry
    public void Install_over_it_is_offered_once()
    {
        var (app, failed) = FailedUpdate(Ea, NoUninstaller);
        app.Dialogs.Answer = false;
        failed.InstallOverCommand.Execute(null);
        Assert.Single(app.Installer.Calls);

        app.Dialogs.Answer = true;
        failed.InstallOverCommand.Execute(null);
        app.Installer.Complete(Ea.Id, NoUninstaller);

        var again = app.Queue.Find(Ea)!;
        Assert.False(again.CanInstallOver);
        Assert.True(again.CanRetry);
    }
}

/// <summary>020 R5.3: which call WinGet gets.</summary>
public class InstallOverCallTests
{
    [Theory]
    [InlineData(OperationKind.Upgrade, true, ScopeSource.Installed, true)]    // Install over it
    [InlineData(OperationKind.Upgrade, true, ScopeSource.Setting, true)]      // whatever the scope's source
    [InlineData(OperationKind.Upgrade, false, ScopeSource.Installed, false)]  // an ordinary update
    [InlineData(OperationKind.Install, true, ScopeSource.Installed, false)]   // only for an update
    public void Install_over_it_runs_the_update_as_an_install(OperationKind kind, bool installOver, ScopeSource source, bool expected) =>
        Assert.Equal(expected, WinGetPackageInstaller.RunsAsInstall(kind, installOver, source, InstallScope.System, InstallScope.System));

    [Fact] // 015 R3.1 unchanged
    public void A_scope_mismatch_still_runs_as_an_install() =>
        Assert.True(WinGetPackageInstaller.RunsAsInstall(OperationKind.Upgrade, false, ScopeSource.Installed, InstallScope.User, InstallScope.System));
}
