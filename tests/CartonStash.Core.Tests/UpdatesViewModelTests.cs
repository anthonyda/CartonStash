using CartonStash.Core.Models;
using CartonStash.Core.ViewModels;

namespace CartonStash.Core.Tests;

/// <summary>Spec 004 (R3 deferred).</summary>
public class UpdatesViewModelTests
{
    private static async Task<(TestApp App, UpdatesViewModel Vm)> CreateLoaded(params PackageInfo[] installed)
    {
        var app = new TestApp();
        app.SetInstalled(installed);
        var vm = new UpdatesViewModel(app.Store, app.Actions, app.Packages, app.Settings, app.ReleaseNotes);
        await vm.LoadAsync();
        return (app, vm);
    }

    [Fact]
    public async Task Lists_only_managed_packages_with_an_update_sorted_by_name() // R1.1
    {
        var (_, vm) = await CreateLoaded(
            Pkg.Installed("Z", "zip", update: true),
            Pkg.Installed("N", "none"),
            Pkg.Installed("A", "alpha", update: true),
            Pkg.Installed("U", "unmanaged", source: ""));

        Assert.Equal(["alpha", "zip"], vm.Updates.Select(r => r.Name));
        Assert.All(vm.Updates, r => Assert.True(r.ShowUpdate));
        Assert.Equal("Updates (2)", vm.Title);
    }

    [Fact]
    public async Task Excluded_rows_move_below_and_leave_the_count_and_update_all() // 010 R5.5, R5.7, R5.9, R5.10
    {
        var (app, vm) = await CreateLoaded(Pkg.Installed("A", "alpha", update: true), Pkg.Installed("B", "beta", update: true));

        vm.Updates[0].ExcludeCommand.Execute(null);

        Assert.Equal(["beta"], vm.Updates.Select(r => r.Name));
        Assert.Equal(["alpha"], vm.Excluded.Select(r => r.Name));
        Assert.True(vm.HasExcluded);
        Assert.Equal(1, vm.Count);
        Assert.Equal("Updates (1)", vm.Title);
        Assert.Equal(["A"], app.Settings.Current.ExcludedPackageIds);

        await vm.UpdateAllCommand.ExecuteAsync(null);

        Assert.Equal(["B"], app.Installer.Calls.Select(c => c.Id));
    }

    [Fact]
    public async Task An_excluded_row_can_still_be_updated_and_included_again() // 010 R5.3, R5.5
    {
        var (app, vm) = await CreateLoaded(Pkg.Installed("A", update: true));
        vm.Updates[0].ExcludeCommand.Execute(null);
        var row = vm.Excluded[0];

        Assert.True(row.ShowUpdate);
        Assert.True(row.ShowIncludeAgain);
        Assert.False(row.ShowExclude);

        row.IncludeAgainCommand.Execute(null);

        Assert.Single(vm.Updates);
        Assert.Empty(vm.Excluded);
        Assert.Empty(app.Settings.Current.ExcludedPackageIds);
    }

    [Fact]
    public async Task Only_excluded_updates_shows_up_to_date() // 010 R5.7
    {
        var (_, vm) = await CreateLoaded(Pkg.Installed("A", update: true));

        vm.Updates[0].ExcludeCommand.Execute(null);

        Assert.True(vm.IsUpToDate);
        Assert.Equal("Updates", vm.Title);
    }

    [Fact]
    public async Task Other_settings_changes_keep_the_selection()
    {
        var (app, vm) = await CreateLoaded(Pkg.Installed("A", update: true));
        vm.SetSelection([vm.Updates[0]]);

        app.Settings.Update(s => s with { Theme = AppTheme.Dark });

        Assert.True(vm.HasSelection);
    }

    [Fact]
    public async Task Shares_the_installed_list() // R1.2
    {
        var app = new TestApp();
        app.SetInstalled(Pkg.Installed("A", update: true));
        var installed = new InstalledViewModel(app.Store, app.Actions, app.Packages, app.ReleaseNotes);
        var updates = new UpdatesViewModel(app.Store, app.Actions, app.Packages, app.Settings, app.ReleaseNotes);

        await installed.LoadAsync();
        await updates.LoadAsync();

        Assert.Equal(1, app.Packages.InstalledCalls);
        Assert.Single(updates.Updates);
    }

    [Fact]
    public async Task Row_update_queues_an_upgrade() // R2.1
    {
        var (app, vm) = await CreateLoaded(Pkg.Installed("A", update: true));

        vm.Updates[0].UpdateCommand.Execute(null);

        Assert.Equal([(OperationKind.Upgrade, "A", false)], app.Installer.Calls);
        Assert.True(vm.Updates[0].IsBusy);
    }

    [Fact]
    public async Task Update_selected_queues_only_the_checked_rows_without_asking() // R2.2
    {
        var (app, vm) = await CreateLoaded(Pkg.Installed("A", update: true), Pkg.Installed("B", update: true), Pkg.Installed("C", update: true));
        Assert.False(vm.UpdateSelectedCommand.CanExecute(null));

        vm.SetSelection([vm.Updates[0], vm.Updates[2]]);
        await vm.UpdateSelectedCommand.ExecuteAsync(null);

        Assert.Empty(app.Dialogs.Confirmations);
        app.Installer.Complete("A");
        Assert.Equal(["A", "C"], app.Installer.Calls.Select(c => c.Id));
    }

    [Fact]
    public async Task Update_all_confirms_then_queues_everything() // R2.3
    {
        var (app, vm) = await CreateLoaded(Pkg.Installed("A", update: true), Pkg.Installed("B", update: true));

        await vm.UpdateAllCommand.ExecuteAsync(null);

        Assert.Single(app.Dialogs.Confirmations);
        Assert.Equal(2, app.Queue.ActiveCount);
    }

    [Fact]
    public async Task Updated_package_leaves_the_list_after_the_reload() // R2.4
    {
        var (app, vm) = await CreateLoaded(Pkg.Installed("A", update: true), Pkg.Installed("B", update: true));
        vm.Updates[0].UpdateCommand.Execute(null);
        app.SetInstalled(Pkg.Installed("A"), Pkg.Installed("B", update: true));

        app.Installer.Complete("A");
        await app.Queue.Idle;

        Assert.Equal(["B"], vm.Updates.Select(r => r.Id));
    }

    [Fact]
    public async Task App_Installer_waiting_for_a_restart_offers_the_restart_instead() // R6.2, R6.4 decision: still counted
    {
        var appInstaller = WinGetHostTests.AppInstaller();
        var (app, vm) = await CreateLoaded(appInstaller, Pkg.Installed("B", update: true));
        vm.Updates.Single(r => r.Id == "Microsoft.AppInstaller").UpdateCommand.Execute(null);

        app.Installer.Complete("Microsoft.AppInstaller", OperationResult.SucceededAppRestartRequired, "Updated. Restart CartonStash to finish.");
        await app.Queue.Idle;

        var row = vm.Updates.Single(r => r.Id == "Microsoft.AppInstaller");
        Assert.False(row.ShowUpdate);
        Assert.True(row.ShowRestart);
        Assert.Equal("Restart CartonStash to finish", row.ResultText);
        Assert.Equal("Updates (2)", vm.Title);

        await vm.UpdateAllCommand.ExecuteAsync(null);
        Assert.Equal(["Microsoft.AppInstaller", "B"], app.Installer.Calls.Select(c => c.Id));
    }

    [Fact]
    public async Task Other_rows_never_offer_the_restart() // R6.2
    {
        var (app, vm) = await CreateLoaded(WinGetHostTests.AppInstaller(), Pkg.Installed("B", update: true));
        app.Actions.Host.SetPending("1.29.379.0");

        Assert.False(vm.Updates.Single(r => r.Id == "B").ShowRestart);
        Assert.True(vm.Updates.Single(r => r.Id == "B").ShowUpdate);
        Assert.True(vm.Updates.Single(r => r.Id == "Microsoft.AppInstaller").ShowRestart);
    }

    [Fact]
    public async Task Only_wingets_own_match_is_offered_for_update() // 002 R6.4
    {
        var primary = Pkg.Installed("Microsoft.WSL", "WSL Update", update: true) with { InstalledId = "ARP-old" };
        var secondary = Pkg.Installed("Microsoft.WSL", "WSL", update: true) with { InstalledId = "MSIX-new", IsPrimaryMatch = false, UpdateAvailable = false };
        var (_, vm) = await CreateLoaded(primary, secondary);

        var row = Assert.Single(vm.Updates);
        Assert.Equal("ARP-old", row.Package.InstalledId);
    }

    [Fact]
    public async Task Rows_sharing_an_id_track_only_their_own_operation() // 002 R6.5
    {
        var app = new TestApp();
        var a = Pkg.Installed("Microsoft.WSL", "WSL Update") with { InstalledId = "ARP-old" };
        var b = Pkg.Installed("Microsoft.WSL", "WSL") with { InstalledId = "MSIX-new", IsPrimaryMatch = false };
        app.SetInstalled(a, b);
        var vm = new InstalledViewModel(app.Store, app.Actions, app.Packages, app.ReleaseNotes);
        await vm.LoadAsync();

        app.Queue.Enqueue(OperationKind.Uninstall, b);

        Assert.True(vm.Visible.Single(r => r.Package.InstalledId == "MSIX-new").IsBusy);
        Assert.False(vm.Visible.Single(r => r.Package.InstalledId == "ARP-old").IsBusy);
    }

    [Fact]
    public async Task Up_to_date_when_nothing_needs_updating() // R5.1
    {
        var (_, vm) = await CreateLoaded(Pkg.Installed("A"));

        Assert.True(vm.IsUpToDate);
        Assert.Equal(0, vm.Count);
    }

    [Fact]
    public void Not_up_to_date_before_loading() // R4.1: no badge, no empty state until loaded
    {
        var app = new TestApp();
        var vm = new UpdatesViewModel(app.Store, app.Actions, app.Packages, app.Settings, app.ReleaseNotes);

        Assert.Null(vm.Count);
        Assert.False(vm.IsUpToDate);
    }
}
