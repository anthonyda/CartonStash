using CartonStash.Core.Models;
using CartonStash.Core.ViewModels;

namespace CartonStash.Core.Tests;

/// <summary>003 R4.2 (BR-07): never the same package twice, across pages.</summary>
public class SamePackageTests
{
    private static readonly PackageInfo VlcInstalled = Pkg.Installed("VideoLAN.VLC", "VLC", update: true) with { InstalledId = @"ARP\Machine\X64\VLC media player" };
    private static readonly PackageInfo VlcSearch = Pkg.Remote("VideoLAN.VLC", "VLC");

    [Fact]
    public async Task An_update_queued_from_Updates_is_seen_from_Search_R4_2()
    {
        var app = new TestApp();
        var searchRow = new PackageRowViewModel(VlcSearch, app.Actions, RowActions.Install);
        RowOperationTracker.Track(app.Actions, () => [searchRow]);

        await app.Actions.UpdateAsync(VlcInstalled);

        Assert.True(searchRow.IsBusy);
        Assert.Same(app.Queue.Find(VlcInstalled), app.Queue.Enqueue(OperationKind.Install, VlcSearch, options: new() { Version = "3.0.20" }));
        Assert.Single(app.Queue.Operations);
        Assert.True(new PackageRowViewModel(VlcSearch, app.Actions, RowActions.Install).IsBusy); // a row built later too
    }

    [Fact]
    public async Task One_queued_from_Search_is_seen_from_Installed_R4_2()
    {
        var app = new TestApp();
        await app.Actions.InstallAsync(VlcSearch with { InstalledVersion = null });

        Assert.Same(app.Queue.Operations[0], app.Queue.Enqueue(OperationKind.Upgrade, VlcInstalled));
    }

    [Fact] // 002 R6.5: two installed copies with one WinGet ID stay apart
    public void Two_installed_copies_stay_apart_002_R6_5()
    {
        var user = VlcInstalled with { InstalledId = @"ARP\User\X64\VLC" };

        Assert.False(VlcInstalled.IsSamePackage(user));
        Assert.True(VlcSearch.IsSamePackage(user));
    }

    [Fact] // the same ID in two sources is two packages
    public void The_same_ID_in_two_sources_stays_two_packages_R4_2()
    {
        Assert.False(VlcSearch.IsSamePackage(VlcSearch with { Source = "corp" }));
        Assert.True(VlcSearch.IsSamePackage(VlcSearch with { Name = "other name" }));
    }
}
