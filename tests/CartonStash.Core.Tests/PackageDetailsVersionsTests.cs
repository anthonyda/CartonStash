using CartonStash.Core.Models;
using CartonStash.Core.ViewModels;

namespace CartonStash.Core.Tests;

/// <summary>014 T8, T14: the version list in the details pane (R1.1, R1.3 to R1.6).</summary>
public class PackageDetailsVersionsTests
{
    private static readonly PackageInfo PowerToys = new("Microsoft.PowerToys", "PowerToys", "0.102.0", "0.105.0", "winget", true);

    /// <summary>0.105.0 down to 0.81.0, with 0.102.0 installed.</summary>
    private static PackageDetails Versions(int count = 25) => new()
    {
        Versions = [.. Enumerable.Range(0, count).Select(i => 105 - i).Select(minor => new PackageVersion($"0.{minor}.0",
            minor > 102 ? VersionDirection.Newer : minor == 102 ? VersionDirection.Installed : VersionDirection.Older))],
    };

    private static async Task<(TestApp App, PackageDetailsViewModel Vm)> Open(PackageInfo package, PackageDetails? details)
    {
        var app = new TestApp();
        app.Packages.Details = (_, _) => Task.FromResult(details);
        var vm = new PackageDetailsViewModel(app.Packages, app.ReleaseNotes);
        var row = new PackageRowViewModel(package, app.Actions, RowActions.All);
        RowOperationTracker.Track(app.Actions, () => [row]); // as the page does for its rows
        await vm.LoadAsync(row);
        return (app, vm);
    }

    private static VersionItem Item(PackageDetailsViewModel vm, string version) => vm.ShownVersions.Single(v => v.Version.Version == version);

    [Fact] // R1.3
    public async Task Ten_are_shown_then_all_then_ten_for_the_next_package()
    {
        var (app, vm) = await Open(PowerToys, Versions());

        Assert.Equal(10, vm.ShownVersions.Count);
        Assert.True(vm.HasMoreVersions);
        vm.ShowAllVersionsCommand.Execute(null);
        Assert.Equal(25, vm.ShownVersions.Count);
        Assert.False(vm.HasMoreVersions);

        await vm.LoadAsync(new PackageRowViewModel(PowerToys, app.Actions, RowActions.All));
        Assert.Equal(10, vm.ShownVersions.Count);
    }

    [Fact] // R1.3, 005 R2.1
    public async Task The_installed_version_stays_marked()
    {
        var (_, vm) = await Open(PowerToys, Versions());

        Assert.Equal("0.102.0 (installed)", Item(vm, "0.102.0").Label);
        Assert.Equal("0.101.0", Item(vm, "0.101.0").Label);
    }

    [Theory] // R1.1, R1.4
    [InlineData("0.103.0", "Update to this version", "Update PowerToys to 0.103.0")]
    [InlineData("0.101.0", "Go back to this version", "Go back to PowerToys 0.101.0")]
    public async Task The_action_reads_the_direction(string version, string text, string name)
    {
        var (_, vm) = await Open(PowerToys, Versions());

        vm.SelectedVersion = Item(vm, version);

        Assert.True(vm.ShowVersionAction);
        Assert.True(vm.CanRunVersion);
        Assert.Equal(text, vm.VersionActionText);
        Assert.Equal(name, vm.VersionActionName);
    }

    [Fact] // R1.1, R1.2
    public async Task Not_installed_and_uncomparable_versions_are_installs()
    {
        var details = new PackageDetails { Versions = [new("2.0", VersionDirection.NotInstalled), new("1.0", VersionDirection.Unknown)] };
        var (_, vm) = await Open(PowerToys, details);

        vm.SelectedVersion = Item(vm, "2.0");
        Assert.Equal("Install", vm.VersionActionText);
        Assert.Equal("Install PowerToys 2.0", vm.VersionActionName);
        vm.SelectedVersion = Item(vm, "1.0");
        Assert.Equal("Install this version", vm.VersionActionText);
    }

    [Fact] // R1.1
    public async Task The_installed_version_has_no_action()
    {
        var (_, vm) = await Open(PowerToys, Versions());

        vm.SelectedVersion = Item(vm, "0.102.0");

        Assert.False(vm.ShowVersionAction);
    }

    [Fact] // R1.5: one version, as the Store source lists
    public async Task One_version_offers_no_actions()
    {
        var (_, vm) = await Open(PowerToys, new PackageDetails { Versions = [new("Unknown", VersionDirection.Unknown)] });

        Assert.False(vm.OffersVersionActions);
        Assert.True(vm.ShowVersionsText);
    }

    [Fact] // R1.5
    public async Task An_unmanaged_package_offers_no_actions()
    {
        var unmanaged = new PackageInfo("ARP\\X", "Something", "1.0", null, "");
        var (_, vm) = await Open(unmanaged, Versions());

        Assert.False(vm.OffersVersionActions);
    }

    [Fact] // R1.5, 003 R4.2
    public async Task Not_while_the_package_is_busy()
    {
        var (app, vm) = await Open(PowerToys, Versions());
        vm.SelectedVersion = Item(vm, "0.103.0");

        await vm.Row!.UpdateCommand.ExecuteAsync(null);
        Assert.False(vm.CanRunVersion);
        Assert.False(vm.RunVersionCommand.CanExecute(null));

        app.Installer.Complete(PowerToys.Id);
        Assert.True(vm.CanRunVersion);
    }

    [Fact] // R1.1
    public async Task The_button_installs_the_selected_version()
    {
        var (app, vm) = await Open(PowerToys, Versions());
        vm.SelectedVersion = Item(vm, "0.103.0");

        await vm.RunVersionCommand.ExecuteAsync(null);

        Assert.Equal([(OperationKind.Upgrade, PowerToys.Id, false)], app.Installer.Calls);
        Assert.Equal(["0.103.0"], app.Installer.Versions);
    }

    [Fact]
    public async Task The_selection_resets_for_another_package()
    {
        var (app, vm) = await Open(PowerToys, Versions());
        vm.SelectedVersion = Item(vm, "0.101.0");

        await vm.LoadAsync(new PackageRowViewModel(PowerToys, app.Actions, RowActions.All));

        Assert.Null(vm.SelectedVersion);
    }

    [Fact] // R1.6
    public async Task VersionAction_ShownOnlyOnSelectedItem_R1_6()
    {
        var (_, vm) = await Open(PowerToys, Versions());
        Assert.DoesNotContain(vm.ShownVersions, v => v.ShowAction);

        vm.SelectedVersion = Item(vm, "0.103.0");
        Assert.Equal(["0.103.0"], vm.ShownVersions.Where(v => v.ShowAction).Select(v => v.Version.Version));

        vm.SelectedVersion = Item(vm, "0.101.0");
        Assert.Equal(["0.101.0"], vm.ShownVersions.Where(v => v.ShowAction).Select(v => v.Version.Version));
        Assert.Equal("Go back to this version", Item(vm, "0.101.0").ActionText);
        Assert.Equal("Go back to PowerToys 0.101.0", Item(vm, "0.101.0").ActionName);

        // Show all keeps the same items, so the selected line keeps its button.
        vm.ShowAllVersionsCommand.Execute(null);
        Assert.Same(vm.SelectedVersion, Item(vm, "0.101.0"));
        Assert.True(Item(vm, "0.101.0").ShowAction);
    }

    [Fact] // R1.6, R1.1, 003 R4.2
    public async Task VersionAction_FollowsBusyAndInstalled_R1_6()
    {
        var (app, vm) = await Open(PowerToys, Versions());

        vm.SelectedVersion = Item(vm, "0.102.0");
        Assert.False(Item(vm, "0.102.0").ShowAction);

        var item = Item(vm, "0.103.0");
        vm.SelectedVersion = item;
        Assert.True(item.RunCommand.CanExecute(null));
        await vm.Row!.UpdateCommand.ExecuteAsync(null);
        Assert.False(item.RunCommand.CanExecute(null));
        app.Installer.Complete(PowerToys.Id);
        Assert.True(item.RunCommand.CanExecute(null));

        await item.RunCommand.ExecuteAsync(null);
        Assert.Equal(["0.103.0"], app.Installer.Versions.Skip(1));
    }
}
