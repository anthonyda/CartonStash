using CartonStash.Core.Models;
using CartonStash.Core.Services;

namespace CartonStash.Core.Tests;

/// <summary>020: Uninstall, then update (R1 to R4), from a failed update's card and row. R5 was withdrawn in 1.7.1.</summary>
public class UninstallThenUpdateTests
{
    private static readonly PackageInfo Miniconda = new("Anaconda.Miniconda3", "Miniconda3", "py312_24.11.1-0", "py314_26.9.4-1", "winget", true)
    {
        InstalledId = @"ARP\User\X64\Miniconda3 py312_24.11.1-0 (Python 3.12.8 64-bit)",
    };

    /// <summary>013 R3.1's result, as the installer maps it now (T3), with the installed scope it asked for.</summary>
    private static readonly OperationOutcome Refused = new(OperationResult.Failed, "Miniconda3 can't be updated in place.")
    {
        Retryable = false,
        OffersUninstallFirst = true,
        Scope = InstallScope.User,
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

    [Fact] // T11: the installed entry's name already carries its version
    public void The_confirmation_does_not_repeat_a_version_the_name_carries()
    {
        var entry = Miniconda with { Name = "Miniconda3 py312_24.11.1-0 (Python 3.12.8 64-bit)" };
        var (app, failed) = FailedUpdate(entry, Refused);
        app.Dialogs.Answer = false;

        failed.UninstallThenUpdateCommand.Execute(null);

        Assert.StartsWith("Uninstall Miniconda3 py312_24.11.1-0 (Python 3.12.8 64-bit), then update to py314_26.9.4-1? | "
            + "Miniconda3 py312_24.11.1-0 (Python 3.12.8 64-bit) will be uninstalled,", Assert.Single(app.Dialogs.Confirmations));
    }

    [Fact] // T11: once the chain ran, the old failed card no longer offers it
    public void A_later_operation_takes_the_offer_off_the_old_card()
    {
        var (app, failed) = FailedUpdate(Miniconda, Refused);
        failed.UninstallThenUpdateCommand.Execute(null);

        Assert.True(failed.IsSuperseded);
        Assert.False(failed.CanUninstallThenUpdate);
        Assert.False(failed.UninstallThenUpdateCommand.CanExecute(null));
    }

    // --- R4.1

    [Fact] // R4.1: a refused update queues nothing by itself
    public void A_refused_update_does_nothing_on_its_own()
    {
        var (app, _) = FailedUpdate(Miniconda, Refused);

        Assert.Single(app.Installer.Calls);
        Assert.Empty(app.Dialogs.Confirmations);
    }
}
