using CartonStash.Core.Models;
using CartonStash.Core.ViewModels;

namespace CartonStash.Core.Tests;

/// <summary>014 T21: rows and the details pane show what WinGet lists after an operation (R2.6).</summary>
public class PackageRowRefreshTests
{
    private static readonly PackageInfo Pandoc310 = new("JohnMacFarlane.Pandoc", "Pandoc", "3.10", "3.13", "winget", UpdateAvailable: true)
    {
        VersionStatus = VersionStatus.Older,
    };

    private static readonly PackageInfo Pandoc313 = Pandoc310 with { InstalledVersion = "3.13", UpdateAvailable = false, VersionStatus = VersionStatus.Same };

    private static (TestApp App, PackageRowViewModel Row) Row(PackageInfo package, RowActions allowed = RowActions.All)
    {
        var app = new TestApp();
        var row = new PackageRowViewModel(package, app.Actions, allowed);
        RowOperationTracker.Track(app.Actions, () => [row]); // as the page does for its rows
        return (app, row);
    }

    [Fact] // the Pandoc finding: updated from Search, the row still said "Installed 3.10"
    public async Task After_a_success_the_row_shows_what_WinGet_lists_R2_6()
    {
        var (app, row) = Row(Pandoc310, RowActions.Install);
        app.Packages.ReadInstalled = _ => Pandoc313;
        var changed = new List<string?>();
        row.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        await app.Actions.UpdateAsync(row.Package);
        app.Installer.Complete(Pandoc310.Id);

        Assert.Equal("3.13", row.Package.InstalledVersion);
        Assert.Equal("Installed 3.13", row.VersionBadgeText);
        Assert.Equal(VersionBadge.Same, row.VersionBadge);
        Assert.Contains(string.Empty, changed); // every binding reads it again
    }

    [Fact] // a failed read is never an error of its own (013 R2.3)
    public async Task A_failed_read_leaves_the_row_R2_6()
    {
        var (app, row) = Row(Pandoc310);
        app.Packages.ReadInstalled = _ => null;

        await app.Actions.UpdateAsync(row.Package);
        app.Installer.Complete(Pandoc310.Id);

        Assert.Same(Pandoc310, row.Package);
        Assert.Equal(1, app.Packages.ReadInstalledCalls);
    }

    [Theory] // only a success is read again
    [InlineData(OperationResult.Failed)]
    [InlineData(OperationResult.Cancelled)]
    public async Task A_failure_reads_nothing_R2_6(OperationResult result)
    {
        var (app, row) = Row(Pandoc310);
        app.Packages.ReadInstalled = _ => Pandoc313;

        await app.Actions.UpdateAsync(row.Package);
        app.Installer.Complete(Pandoc310.Id, result, "no");

        Assert.Same(Pandoc310, row.Package);
        Assert.Equal(0, app.Packages.ReadInstalledCalls);
    }

    [Fact] // the installed entry ID stays: it's the row's identity (002 R6.5)
    public void Refresh_keeps_the_row_and_its_identity_R2_6()
    {
        var installed = Pandoc310 with { InstalledId = @"ARP\Machine\X64\{X}" };
        var (_, row) = Row(installed);

        row.Refresh(Pandoc313 with { InstalledId = installed.InstalledId });

        Assert.Equal(installed.Key, row.Package.Key);
    }

    [Fact] // nothing changed: no notification
    public void Refreshing_with_the_same_package_raises_nothing_R2_6()
    {
        var (_, row) = Row(Pandoc310);
        var changed = 0;
        row.PropertyChanged += (_, _) => changed++;

        row.Refresh(Pandoc310 with { });

        Assert.Equal(0, changed);
    }

    [Fact] // the pane reloads its versions against the new installed version
    public async Task The_open_pane_reloads_when_its_row_changes_R2_6()
    {
        var (app, row) = Row(Pandoc310);
        var loads = new List<string?>();
        app.Packages.Details = (p, _) =>
        {
            loads.Add(p.InstalledVersion);
            return Task.FromResult<PackageDetails?>(new() { Versions = [new("3.13", VersionDirection.Newer), new("3.10", VersionDirection.Installed)] });
        };
        var pane = new PackageDetailsViewModel(app.Packages, app.ReleaseNotes);
        await pane.LoadAsync(row);

        row.Refresh(Pandoc313);
        await Task.Yield();

        Assert.Equal(["3.10", "3.13"], loads);
        Assert.Same(row, pane.Row);
    }

    [Fact]
    public async Task The_pane_doesnt_reload_for_another_row_or_when_the_version_is_the_same_R2_6()
    {
        var (app, row) = Row(Pandoc310);
        var loads = 0;
        app.Packages.Details = (_, _) => { loads++; return Task.FromResult<PackageDetails?>(null); };
        var pane = new PackageDetailsViewModel(app.Packages, app.ReleaseNotes);
        await pane.LoadAsync(row);
        var other = new PackageRowViewModel(Pkg.Installed("B"), app.Actions, RowActions.All);
        await pane.LoadAsync(other);

        row.Refresh(Pandoc313); // not the pane's row any more
        other.Refresh(other.Package with { AvailableVersion = "9.9" }); // same installed version

        Assert.Equal(2, loads);
    }
}
