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

    private static readonly PackageInfo OpenSsl4 = new("FireDaemon.OpenSSL", "FireDaemon OpenSSL 4", "4.1.0", "4.1.0", "winget")
    {
        InstalledId = @"ARP\Machine\X64\{5BA06AD7-0000-0000-0000-000000000000}",
    };

    // Option G, as the wordings page shows it.
    private const string Named = "WinGet lists these programs under FireDaemon.OpenSSL:\n"
        + "• FireDaemon OpenSSL 3 (3.6.2), this one\n"
        + "• FireDaemon OpenSSL 4 (4.1.0)\n\n"
        + "It doesn't say which one 4.1.0 replaces, so it may be installed next to FireDaemon OpenSSL 3. "
        + "If both stay, you can uninstall 3.6.2 from the Installed page.";

    [Fact]
    public void The_confirmation_names_the_programs_sharing_the_ID_R3_10()
    {
        var (title, message, confirm) = PackageActions.SharedIdQuestion(OpenSsl3, new("4.1.0", VersionDirection.Unknown), [OpenSsl4]);

        Assert.Equal("Install version 4.1.0?", title);
        Assert.Equal(Named, message);
        Assert.Equal("Install 4.1.0", confirm);
        Assert.DoesNotContain("over it", message);
        Assert.DoesNotContain("older", message);
    }

    [Fact]
    public void The_scope_notice_ends_the_last_paragraph_R3_10()
    {
        var (_, message, _) = PackageActions.SharedIdQuestion(OpenSsl3, new("4.1.0", VersionDirection.Unknown), [OpenSsl4], "Notice.");

        Assert.Equal(Named + " Notice.", message);
    }

    [Fact]
    public void A_program_without_a_version_has_no_brackets_R3_10()
    {
        var (_, message, _) = PackageActions.SharedIdQuestion(OpenSsl3, new("4.1.0", VersionDirection.Unknown),
            [OpenSsl4 with { InstalledVersion = null }]);

        Assert.Contains("\n• FireDaemon OpenSSL 4\n\n", message);
    }

    [Fact] // the Installed list isn't loaded: option A
    public void With_no_other_program_known_the_short_form_R3_10()
    {
        var (_, message, _) = PackageActions.SharedIdQuestion(OpenSsl3, new("4.1.0", VersionDirection.Unknown), [], "Notice.");

        Assert.Equal("4.1.0 may be installed next to FireDaemon OpenSSL 3 (3.6.2) instead of replacing it. "
            + "If both stay, you can uninstall 3.6.2 from the Installed page. Notice.\n\n"
            + "WinGet lists several programs under FireDaemon.OpenSSL and doesn't say which one a version replaces.", message);
    }

    [Fact]
    public void Sharing_ID_takes_the_other_installed_programs_by_name_R3_10()
    {
        var zed = OpenSsl4 with { Name = "FireDaemon OpenSSL Zed", InstalledId = "z" };
        var otherId = OpenSsl4 with { Id = "Someone.Else", InstalledId = "x" };
        var notInstalled = OpenSsl4 with { InstalledVersion = null, InstalledId = "n" };
        var ownInAnotherCase = OpenSsl3 with { Id = "firedaemon.openssl", InstalledId = OpenSsl3.InstalledId!.ToLowerInvariant() };

        var others = PackageActions.SharingId(OpenSsl3, [zed, otherId, ownInAnotherCase, notInstalled, OpenSsl4]);

        Assert.Equal(["FireDaemon OpenSSL 4", "FireDaemon OpenSSL Zed"], others.Select(p => p.Name));
    }

    [Fact]
    public async Task A_shared_ID_row_gets_the_plain_confirmation_and_no_box_R3_10()
    {
        var app = new TestApp();
        app.Packages.Installed = _ => Task.FromResult<IReadOnlyList<PackageInfo>>([OpenSsl3, OpenSsl4]);
        await app.Store.ReloadAsync();

        await app.Actions.InstallVersionAsync(OpenSsl3, new("4.1.0", VersionDirection.Unknown));

        Assert.Empty(app.Dialogs.OptionPrompts);
        Assert.Equal(["Install version 4.1.0? | " + Named], app.Dialogs.Confirmations);
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
