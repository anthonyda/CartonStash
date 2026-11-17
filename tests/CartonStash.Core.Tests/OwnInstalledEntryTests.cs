using CartonStash.Core.Models;
using CartonStash.Core.Services;
using CartonStash.Core.ViewModels;

namespace CartonStash.Core.Tests;

/// <summary>
/// 014 T27: the fresh read of R2.6 and R2.7 is of the row's own installed program (R2.9). The case is FireDaemon's
/// OpenSSL 3 and 4 under one ID: WinGet's match is OpenSSL 4, the OpenSSL 3 row is a manifest match (T26).
/// </summary>
public class OwnInstalledEntryTests
{
    private const string OpenSsl3Entry = @"ARP\Machine\X64\{0E95226A-DF2C-4E41-9E57-457C5579DCFA}";
    private const string OpenSsl4Entry = @"ARP\Machine\X64\{5BA06AD7-3B7B-451D-BB85-79864BB17520}";

    private static readonly PackageInfo OpenSsl3 = new("FireDaemon.OpenSSL", "FireDaemon OpenSSL 3", "3.6.2", "4.1.0", "winget")
    {
        InstalledId = OpenSsl3Entry,
        IsPrimaryMatch = false,
    };

    // --- OwnMatch

    [Fact]
    public void Own_match_finds_the_rows_entry_wherever_it_is_R2_9()
    {
        Assert.Equal(0, WinGetPackageService.OwnMatch([OpenSsl3Entry, OpenSsl4Entry], OpenSsl3Entry));
        Assert.Equal(1, WinGetPackageService.OwnMatch([OpenSsl4Entry, OpenSsl3Entry], OpenSsl3Entry));
    }

    [Fact]
    public void Own_match_ignores_case_R2_9() =>
        Assert.Equal(0, WinGetPackageService.OwnMatch([OpenSsl3Entry.ToLowerInvariant()], OpenSsl3Entry));

    [Fact] // T26: WinGet's only match for the ID is OpenSSL 4
    public void Another_programs_match_is_not_the_rows_R2_9() =>
        Assert.Equal(-1, WinGetPackageService.OwnMatch([OpenSsl4Entry], OpenSsl3Entry));

    [Fact]
    public void Matches_without_an_installed_entry_are_skipped_R2_9()
    {
        Assert.Equal(1, WinGetPackageService.OwnMatch([null, OpenSsl3Entry], OpenSsl3Entry));
        Assert.Equal(-1, WinGetPackageService.OwnMatch([null], OpenSsl3Entry));
        Assert.Equal(-1, WinGetPackageService.OwnMatch([], OpenSsl3Entry));
    }

    // --- FromOwnEntry

    [Fact]
    public void Own_entry_gives_name_version_and_scope_and_keeps_the_rest_of_the_row_R2_9()
    {
        var row = OpenSsl3 with { UpdateAvailable = true, VersionStatus = VersionStatus.Older };

        var fresh = WinGetPackageService.FromOwnEntry(row, "FireDaemon OpenSSL 3", "3.6.3", InstallScope.System);

        Assert.Equal("FireDaemon OpenSSL 3", fresh.Name);
        Assert.Equal("3.6.3", fresh.InstalledVersion);
        Assert.Equal(InstallScope.System, fresh.InstalledScope);
        Assert.Equal(row.Id, fresh.Id);
        Assert.Equal(OpenSsl3Entry, fresh.InstalledId);
        Assert.Equal("winget", fresh.Source);
        Assert.Equal("4.1.0", fresh.AvailableVersion);
        Assert.True(fresh.UpdateAvailable);
        Assert.False(fresh.IsPrimaryMatch);
        Assert.Equal(VersionStatus.Older, fresh.VersionStatus);
    }

    // --- R2.7 with the own entry (the fake service stands for ReadInstalledAsync)

    private static async Task<(TestApp App, PackageRowViewModel Row)> InstallFourFromThree(Func<PackageInfo, PackageInfo?> read)
    {
        var app = new TestApp();
        app.Packages.ReadInstalled = read;
        var row = new PackageRowViewModel(OpenSsl3, app.Actions, RowActions.All);
        await row.InstallVersionAsync(new PackageVersion("4.1.0", VersionDirection.Unknown));
        return (app, row);
    }

    [Fact] // the recording: nothing changed outside the app
    public async Task Own_entry_at_the_shown_version_goes_on_R2_9()
    {
        var (app, row) = await InstallFourFromThree(p => WinGetPackageService.FromOwnEntry(p, "FireDaemon OpenSSL 3", "3.6.2", InstallScope.System));

        Assert.Single(app.Dialogs.Confirmations); // R3.10: the shared-ID confirmation
        Assert.Single(app.Installer.Calls);
        Assert.Equal("FireDaemon OpenSSL 3", row.Package.Name);
        Assert.Equal("3.6.2", row.Package.InstalledVersion);
    }

    [Fact] // Q9: OpenSSL 3 changed outside the app since the list loaded
    public async Task Own_entry_at_another_version_queues_nothing_and_refreshes_the_row_R2_9()
    {
        var (app, row) = await InstallFourFromThree(p => WinGetPackageService.FromOwnEntry(p, "FireDaemon OpenSSL 3", "3.6.3", InstallScope.System));

        Assert.Empty(app.Dialogs.Confirmations);
        Assert.Empty(app.Installer.Calls);
        Assert.Equal("FireDaemon OpenSSL 3", row.Package.Name);
        Assert.Equal("3.6.3", row.Package.InstalledVersion);
        Assert.Equal(OpenSsl3Entry, row.Package.InstalledId);
    }

    [Fact] // the own entry is gone: a read that couldn't be made
    public async Task No_own_entry_goes_on_with_what_the_pane_shows_R2_9()
    {
        var (app, row) = await InstallFourFromThree(_ => null);

        Assert.Single(app.Dialogs.Confirmations); // R3.10
        Assert.Single(app.Installer.Calls);
        Assert.Equal("3.6.2", row.Package.InstalledVersion);
    }
}
