using CartonStash.Core.Models;
using CartonStash.Core.ViewModels;

namespace CartonStash.Core.Tests;

/// <summary>
/// BR-04: a success hides the row's button only until the list shows WinGet's data again (003 R3.1, 004 R2.4). A row
/// built later, or refreshed by a reload, follows the package alone.
/// </summary>
public class RowFinishedOperationTests
{
    [Fact] // install X from Search, uninstall it elsewhere, search again: the new row offers Install
    public async Task A_search_row_built_after_an_install_still_offers_install_003_R3_1()
    {
        var app = new TestApp();
        var x = Pkg.Remote("X");
        app.Packages.ReadInstalled = _ => null;
        await app.Actions.InstallAsync(x);
        app.Installer.Complete("X");

        var row = new PackageRowViewModel(x, app.Actions, RowActions.Install);

        Assert.True(row.ShowInstall);
    }

    [Fact] // the row that ran it: the button stays hidden until the list reloads
    public async Task The_row_that_ran_the_install_hides_install_until_a_reload_003_R3_1()
    {
        var app = new TestApp();
        var x = Pkg.Remote("X");
        app.Packages.ReadInstalled = _ => null; // the read after success fails: the row keeps its old package
        var row = new PackageRowViewModel(x, app.Actions, RowActions.Install);
        RowOperationTracker.Track(app.Actions, () => [row]);

        await app.Actions.InstallAsync(x);
        app.Installer.Complete("X");

        Assert.False(row.ShowInstall);
        Assert.Equal("Installed", row.ResultText);
    }

    [Fact] // "Update not confirmed" (013 R2.2): after the reload the row still has an update, so it offers Update again
    public async Task An_updates_row_refreshed_by_a_reload_offers_update_again_004_R2_4()
    {
        var app = new TestApp();
        var x = Pkg.Installed("X", update: true) with { InstalledId = @"ARP\Machine\X64\X" };
        app.Packages.ReadInstalled = _ => null;
        var row = new PackageRowViewModel(x, app.Actions, RowActions.Update);
        RowOperationTracker.Track(app.Actions, () => [row]);
        await app.Actions.UpdateAsync(x);
        app.Installer.Complete("X", OperationResult.SucceededUnconfirmed, "still 1.0");
        Assert.False(row.ShowUpdate);
        var shown = new List<string?>();
        row.PropertyChanged += (_, e) => shown.Add(e.PropertyName);

        row.Refresh(x with { }); // the reload lists the same package (UpdatesViewModel.Rebuild)

        Assert.True(row.ShowUpdate);
        Assert.Contains(nameof(PackageRowViewModel.ShowUpdate), shown);
    }

    [Fact] // a new Updates row for a package whose update finished earlier
    public async Task An_updates_row_built_after_an_update_offers_update_004_R2_4()
    {
        var app = new TestApp();
        var x = Pkg.Installed("X", update: true) with { InstalledId = @"ARP\Machine\X64\X" };
        app.Packages.ReadInstalled = _ => null;
        await app.Actions.UpdateAsync(x);
        app.Installer.Complete("X");

        var row = new PackageRowViewModel(x, app.Actions, RowActions.Update);

        Assert.True(row.ShowUpdate);
    }

    [Fact] // a running operation still takes the buttons away from a new row (R4.2)
    public async Task A_new_row_still_gives_way_to_a_running_operation_003_R4_2()
    {
        var app = new TestApp();
        var x = Pkg.Remote("X");
        await app.Actions.InstallAsync(x);

        var row = new PackageRowViewModel(x, app.Actions, RowActions.Install);

        Assert.True(row.IsBusy);
        Assert.False(row.ShowInstall);
        app.Installer.Complete("X");
        Assert.False(row.ShowInstall); // it saw this one run
    }
}
