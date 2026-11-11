using CartonStash.Core.Models;
using CartonStash.Core.ViewModels;

namespace CartonStash.Core.Tests;

/// <summary>015 T6: the details pane's scope choice and "Installed for …" line (R1.1, R1.3, R1.7, R2.1, R2.2).</summary>
public class PackageDetailsScopeTests
{
    private static readonly PackageInfo Remote = new("JohnMacFarlane.Pandoc", "Pandoc", null, "3.13", "winget");
    private static readonly PackageInfo Installed = Remote with { InstalledVersion = "3.12", UpdateAvailable = true };

    private static async Task<(TestApp App, PackageDetailsViewModel Vm, PackageRowViewModel Row)> Open(PackageInfo package, PackageDetails details)
    {
        var app = new TestApp();
        app.Packages.Details = (_, _) => Task.FromResult<PackageDetails?>(details);
        var vm = new PackageDetailsViewModel(app.Packages, app.ReleaseNotes);
        var row = new PackageRowViewModel(package, app.Actions, RowActions.All);
        RowOperationTracker.Track(app.Actions, () => [row]);
        await vm.LoadAsync(row);
        return (app, vm, row);
    }

    [Fact]
    public async Task The_pane_offers_both_scopes_when_the_details_say_so_R1_1()
    {
        var (_, vm, _) = await Open(Remote, new() { OffersScopeChoice = true });

        Assert.True(vm.OffersScopeChoice);
    }

    [Fact] // installed: updates and going back keep the installed scope, nothing to choose
    public async Task Not_for_an_installed_package_R1_1()
    {
        var (_, vm, _) = await Open(Installed, new() { OffersScopeChoice = true });

        Assert.False(vm.OffersScopeChoice);
    }

    [Fact]
    public async Task Not_when_WinGet_has_one_scope_R1_1()
    {
        var (_, vm, _) = await Open(Remote, new() { OffersScopeChoice = false });

        Assert.False(vm.OffersScopeChoice);
    }

    [Theory]
    [InlineData(InstallScope.System, "Install Pandoc for all users")]
    [InlineData(InstallScope.User, "Install Pandoc for the current user")]
    public async Task The_menu_installs_in_that_scope_R1_1(InstallScope scope, string title)
    {
        var (app, vm, _) = await Open(Remote, new() { OffersScopeChoice = true });

        await (scope == InstallScope.System ? vm.InstallForAllUsersCommand : vm.InstallForCurrentUserCommand).ExecuteAsync(null);

        Assert.Equal(scope, app.Installer.Scopes[^1]);
        Assert.Equal(title, app.Queue.Find(Remote)!.Title);
    }

    [Fact] // rows have no scope choice: their Install keeps using the setting
    public async Task A_row_install_uses_the_setting_R1_7()
    {
        var (app, _, row) = await Open(Remote, new() { OffersScopeChoice = true });

        await row.InstallCommand.ExecuteAsync(null);

        Assert.Null(app.Installer.Scopes[^1]);
    }

    // --- R1.3: a version from the list

    private static PackageDetails ThreeVersions(VersionDirection direction) => new()
    {
        Versions = [new("3.13", direction), new("3.12", direction), new("3.11", direction)],
    };

    [Fact]
    public async Task A_selected_version_asks_once_and_offers_both_scopes_R1_3()
    {
        var (app, vm, _) = await Open(Remote, ThreeVersions(VersionDirection.NotInstalled));
        app.Packages.ScopeChoice = (_, v) => v == "3.12";
        var item = vm.ShownVersions.Single(i => i.Version.Version == "3.12");

        vm.SelectedVersion = item;
        await Task.Yield();
        vm.SelectedVersion = vm.ShownVersions[0];
        vm.SelectedVersion = item;
        await Task.Yield();

        Assert.True(item.OffersScopeChoice);
        Assert.True(item.ShowScopeChoice);
        Assert.False(item.ShowPlainAction);
        Assert.Equal(["3.12", "3.13"], app.Packages.ScopeChoiceAsked); // 3.12 once, cached
        Assert.Equal("Install Pandoc 3.12 for all users", item.ForAllUsersName);
    }

    [Fact]
    public async Task The_line_installs_that_version_in_the_scope_R1_3()
    {
        var (app, vm, _) = await Open(Remote, ThreeVersions(VersionDirection.NotInstalled));
        app.Packages.ScopeChoice = (_, _) => true;
        vm.SelectedVersion = vm.ShownVersions.Single(i => i.Version.Version == "3.12");
        await Task.Yield();

        await vm.RunVersionForCurrentUserCommand.ExecuteAsync(null);

        Assert.Equal("3.12", app.Installer.Versions[^1]);
        Assert.Equal(InstallScope.User, app.Installer.Scopes[^1]);
    }

    [Fact] // an installed package's versions update or go back: no scope check
    public async Task Versions_of_an_installed_package_ask_nothing_R1_3()
    {
        var (app, vm, _) = await Open(Installed, ThreeVersions(VersionDirection.Older));

        vm.SelectedVersion = vm.ShownVersions[1];
        await Task.Yield();

        Assert.Empty(app.Packages.ScopeChoiceAsked);
        Assert.False(vm.ShownVersions[1].ShowScopeChoice);
    }

    // --- R2

    [Theory]
    [InlineData(InstallScope.System, "Installed for all users")]
    [InlineData(InstallScope.User, "Installed for the current user")]
    public async Task The_pane_says_how_it_is_installed_R2_1(InstallScope scope, string text)
    {
        var (_, vm, _) = await Open(Installed, new() { InstalledScope = scope });

        Assert.Equal(text, vm.InstalledScopeText);
        Assert.True(vm.HasInstalledScope);
    }

    [Fact] // nothing knows: nothing shown, no guess
    public async Task Unknown_shows_nothing_R2_2()
    {
        var (_, vm, _) = await Open(Installed, new() { InstalledScope = null });

        Assert.Null(vm.InstalledScopeText);
        Assert.False(vm.HasInstalledScope);
    }
}
