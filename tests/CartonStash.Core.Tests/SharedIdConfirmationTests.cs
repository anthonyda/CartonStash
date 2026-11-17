using CartonStash.Core.Models;
using CartonStash.Core.Services;
using CartonStash.Core.ViewModels;

namespace CartonStash.Core.Tests;

/// <summary>014 R3.10, Q10, Q11: a version chosen on a row that isn't WinGet's own match for its ID.</summary>
public class SharedIdConfirmationTests
{
    private static readonly PackageInfo OpenSsl3 = new("FireDaemon.OpenSSL", "FireDaemon OpenSSL 3", "3.6.2", "4.1.0", "winget")
    {
        InstalledId = @"ARP\Machine\X64\{0E95226A-0000-0000-0000-000000000000}",
        IsPrimaryMatch = false,
    };

    // The reason, in a line of its own (R3.10, amended after T32).
    private const string Why = "\n\nWhy: WinGet lists FireDaemon OpenSSL 3 under the same ID as another program (FireDaemon.OpenSSL), "
        + "so CartonStash can't tell which one 4.1.0 is for.";

    [Fact]
    public void The_confirmation_is_short_and_promises_nothing_R3_10()
    {
        var (title, message, confirm) = PackageActions.SharedIdQuestion(OpenSsl3, new("4.1.0", VersionDirection.Unknown));

        Assert.Equal("Install version 4.1.0?", title);
        Assert.Equal("4.1.0 may be installed next to FireDaemon OpenSSL 3 (3.6.2) instead of replacing it. "
            + "If both stay, you can uninstall 3.6.2 from the Installed page." + Why, message);
        Assert.Equal("Install 4.1.0", confirm);
        Assert.DoesNotContain("over it", message);
        Assert.DoesNotContain("older", message);
    }

    [Fact]
    public void The_scope_notice_is_added_R3_10()
    {
        var (_, message, _) = PackageActions.SharedIdQuestion(OpenSsl3, new("4.1.0", VersionDirection.Unknown), "Notice.");

        Assert.EndsWith("from the Installed page. Notice." + Why, message);
    }

    [Fact]
    public async Task A_shared_ID_row_gets_the_plain_confirmation_and_no_box_R3_10()
    {
        var app = new TestApp();

        await app.Actions.InstallVersionAsync(OpenSsl3, new("4.1.0", VersionDirection.Unknown));

        Assert.Empty(app.Dialogs.OptionPrompts);
        Assert.Equal(["Install version 4.1.0? | 4.1.0 may be installed next to FireDaemon OpenSSL 3 (3.6.2) instead of replacing it. "
            + "If both stay, you can uninstall 3.6.2 from the Installed page." + Why], app.Dialogs.Confirmations);
        Assert.Equal(OperationKind.Install, app.Installer.Calls.Single().Kind);
        Assert.Equal(["4.1.0"], app.Installer.Versions);
    }

    [Fact] // Q11: the ID's updates are another program's
    public async Task A_shared_ID_row_never_adds_an_exclusion_R3_10()
    {
        var app = new TestApp();

        await app.Actions.InstallVersionAsync(OpenSsl3, new("4.1.0", VersionDirection.Unknown));
        app.Installer.Complete(OpenSsl3.Id);

        Assert.False(app.Settings.Current.IsExcluded(OpenSsl3.Id));
    }

    [Fact]
    public async Task Cancel_queues_nothing_R3_10()
    {
        var app = new TestApp();
        app.Dialogs.Answer = false;

        await app.Actions.InstallVersionAsync(OpenSsl3, new("4.1.0", VersionDirection.Unknown));

        Assert.Empty(app.Queue.Operations);
    }

    [Fact] // R1.2 unchanged for WinGet's own match
    public async Task A_primary_row_WinGet_cant_compare_keeps_the_box_R3_10()
    {
        var app = new TestApp();
        var primary = OpenSsl3 with { IsPrimaryMatch = true };

        await app.Actions.InstallVersionAsync(primary, new("4.1.0", VersionDirection.Unknown));

        var prompt = Assert.Single(app.Dialogs.OptionPrompts);
        Assert.Equal("Install FireDaemon OpenSSL 3 4.1.0?", prompt.Title);
        Assert.Equal("Leave FireDaemon OpenSSL 3 out of update checks", prompt.Option);
        Assert.Empty(app.Dialogs.Confirmations);
    }

    [Theory] // R3.11: which installed program the installer works from
    [InlineData(true, true, false)]   // WinGet's own match
    [InlineData(true, false, false)]  // no entry (a Search row)
    [InlineData(false, true, true)]   // a manifest match: its own entry
    [InlineData(false, false, false)]
    public void Only_a_row_that_isnt_WinGets_match_uses_its_own_entry_R3_11(bool primary, bool hasEntry, bool usesOwn)
    {
        var row = OpenSsl3 with { IsPrimaryMatch = primary, InstalledId = hasEntry ? OpenSsl3.InstalledId : null };

        Assert.Equal(usesOwn, WinGetPackageInstaller.UsesOwnEntry(row));
    }
}
