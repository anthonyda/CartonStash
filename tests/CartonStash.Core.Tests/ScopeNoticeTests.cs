using CartonStash.Core.Models;
using CartonStash.Core.ViewModels;

namespace CartonStash.Core.Tests;

/// <summary>014 T22: the installed version is read again before acting (R2.7), and the confirmation says when the installed scope overrides Settings (R3.8, Q7).</summary>
public class ScopeNoticeTests
{
    private static readonly PackageInfo Pandoc = new("JohnMacFarlane.Pandoc", "Pandoc", "3.13", "3.13", "winget");

    private static readonly PackageVersion Back = new("3.12", VersionDirection.Older);

    private static async Task<(TestApp App, PackageRowViewModel Row)> GoBack(PackageInfo? fresh, InstallScope setting = InstallScope.Any)
    {
        var app = new TestApp();
        app.Settings.Update(s => s with { InstallScope = setting });
        app.Packages.ReadInstalled = _ => fresh;
        var row = new PackageRowViewModel(Pandoc, app.Actions, RowActions.All);
        await row.InstallVersionAsync(Back);
        return (app, row);
    }

    // --- R2.7

    [Fact] // the Pandoc finding: the pane was made for 3.10 while 3.13 was installed
    public async Task Another_installed_version_asks_nothing_and_refreshes_the_row_R2_7()
    {
        var (app, row) = await GoBack(Pandoc with { InstalledVersion = "3.10" });

        Assert.Empty(app.Dialogs.OptionPrompts);
        Assert.Empty(app.Installer.Calls);
        Assert.Equal("3.10", row.Package.InstalledVersion);
    }

    [Fact]
    public async Task The_same_version_goes_on_as_before_R2_7()
    {
        var (app, _) = await GoBack(Pandoc with { });

        Assert.Single(app.Dialogs.OptionPrompts);
        Assert.Single(app.Installer.Calls);
    }

    [Fact] // a failed read goes on with what the pane shows
    public async Task A_failed_read_goes_on_as_before_R2_7()
    {
        var (app, _) = await GoBack(null);

        Assert.Single(app.Dialogs.OptionPrompts);
        Assert.Single(app.Installer.Calls);
    }

    [Fact] // nothing installed: nothing to read
    public async Task A_first_install_reads_nothing_R2_7()
    {
        var app = new TestApp();
        await app.Actions.InstallVersionAsync(Pkg.Remote("B"), new("1.0", VersionDirection.NotInstalled));

        Assert.Equal(0, app.Packages.ReadInstalledCalls);
        Assert.Single(app.Installer.Calls);
    }

    // --- R3.8, Q7

    [Theory] // "Let WinGet decide": no choice of the user's is overridden
    [InlineData(InstallScope.User)]
    [InlineData(InstallScope.System)]
    [InlineData(null)]
    public void No_notice_with_let_WinGet_decide_R3_8(InstallScope? installed)
    {
        Assert.Null(PackageActions.ScopeNotice("Pandoc", "3.12", InstallScope.Any, installed));
    }

    [Theory]
    [InlineData(InstallScope.User)]
    [InlineData(InstallScope.System)]
    public void No_notice_when_the_scopes_match_or_the_installed_one_is_unknown_R3_8(InstallScope setting)
    {
        Assert.Null(PackageActions.ScopeNotice("Pandoc", "3.12", setting, setting));
        Assert.Null(PackageActions.ScopeNotice("Pandoc", "3.12", setting, null));
        Assert.Null(PackageActions.ScopeNotice("Pandoc", "3.12", setting, InstallScope.Any));
    }

    [Fact]
    public void The_notice_names_the_installed_scope_R3_8()
    {
        Assert.Equal("Pandoc is installed for all users, so 3.12 will be installed for all users too, not for the current user as set in Settings.",
            PackageActions.ScopeNotice("Pandoc", "3.12", InstallScope.User, InstallScope.System));
        Assert.Equal("Pandoc is installed for the current user, so 3.12 will be installed for the current user too, not for all users as set in Settings.",
            PackageActions.ScopeNotice("Pandoc", "3.12", InstallScope.System, InstallScope.User));
    }

    [Fact] // T24's case: Settings on "Current user", Pandoc for all users
    public async Task The_confirmation_carries_the_notice_R3_8()
    {
        var (app, _) = await GoBack(Pandoc with { InstalledScope = InstallScope.System }, InstallScope.User);

        Assert.EndsWith(" Pandoc is installed for all users, so 3.12 will be installed for all users too, not for the current user as set in Settings.",
            Assert.Single(app.Dialogs.OptionPrompts).Message);
    }

    [Fact]
    public async Task Without_a_notice_the_confirmation_is_as_before_R3_8()
    {
        var (app, _) = await GoBack(Pandoc with { InstalledScope = InstallScope.User }, InstallScope.User);

        Assert.EndsWith("settings may not carry over to an older version.", Assert.Single(app.Dialogs.OptionPrompts).Message);
    }
}
