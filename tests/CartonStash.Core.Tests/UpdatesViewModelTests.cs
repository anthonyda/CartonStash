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
        var vm = new UpdatesViewModel(app.Store, app.Actions, app.Packages);
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
    public async Task Shares_the_installed_list() // R1.2
    {
        var app = new TestApp();
        app.SetInstalled(Pkg.Installed("A", update: true));
        var installed = new InstalledViewModel(app.Store, app.Actions, app.Packages);
        var updates = new UpdatesViewModel(app.Store, app.Actions, app.Packages);

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
        var vm = new InstalledViewModel(app.Store, app.Actions, app.Packages);
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
        var vm = new UpdatesViewModel(app.Store, app.Actions, app.Packages);

        Assert.Null(vm.Count);
        Assert.False(vm.IsUpToDate);
    }
}
