using CartonStash.Core.Models;
using CartonStash.Core.Services;

namespace CartonStash.Core.Tests;

/// <summary>015 T4: a scope chosen for one install travels to WinGet, and only for that install (R1.4, R1.5).</summary>
public class ChosenScopeTests
{
    private static readonly PackageInfo Pandoc = new("JohnMacFarlane.Pandoc", "Pandoc", null, "3.13", "winget");

    [Theory]
    [InlineData(InstallScope.System, "Install Pandoc for all users")]
    [InlineData(InstallScope.User, "Install Pandoc for the current user")]
    public async Task The_chosen_scope_reaches_WinGet_and_names_the_card_R1_5(InstallScope scope, string title)
    {
        var app = new TestApp();

        await app.Actions.InstallAsync(Pandoc, scope);

        Assert.Equal(scope, app.Installer.Scopes[^1]);
        Assert.Equal(ScopeSource.Chosen, app.Installer.ScopeSources[^1]);
        Assert.Equal(title, app.Queue.Find(Pandoc)!.Title);
        Assert.Equal(InstallScope.Any, app.Settings.Current.InstallScope); // the setting doesn't change
    }

    [Fact]
    public async Task A_plain_install_chooses_nothing_R1_5()
    {
        var app = new TestApp();

        await app.Actions.InstallAsync(Pandoc);

        Assert.Null(app.Installer.Scopes[^1]);
        Assert.Equal("Install Pandoc", app.Queue.Find(Pandoc)!.Title);
    }

    [Fact]
    public async Task Retry_keeps_the_chosen_scope_R1_5()
    {
        var app = new TestApp();
        await app.Actions.InstallAsync(Pandoc, InstallScope.System);
        var operation = app.Queue.Find(Pandoc)!;
        app.Installer.Complete(Pandoc.Id, OperationResult.Failed, "no");

        operation.RetryCommand.Execute(null);

        Assert.Equal(2, app.Installer.Scopes.Count);
        Assert.Equal(InstallScope.System, app.Installer.Scopes[^1]);
        Assert.Equal(ScopeSource.Chosen, app.Installer.ScopeSources[^1]);
    }

    [Fact] // R1.3: a version from the list, not installed
    public async Task A_chosen_version_carries_the_scope_too_R1_5()
    {
        var app = new TestApp();

        await app.Actions.InstallVersionAsync(Pandoc, new("3.12", VersionDirection.NotInstalled), scope: InstallScope.User);

        Assert.Equal(InstallScope.User, app.Installer.Scopes[^1]);
        Assert.Equal("Install Pandoc 3.12 for the current user", app.Queue.Find(Pandoc)!.Title);
    }

    [Fact] // only a first install chooses: going back keeps the installed scope (014 R3.8)
    public async Task Going_back_ignores_a_chosen_scope_R1_5()
    {
        var app = new TestApp();
        var installed = Pandoc with { InstalledVersion = "3.13" };

        await app.Actions.InstallVersionAsync(installed, new("3.12", VersionDirection.Older), scope: InstallScope.System);

        Assert.Null(app.Installer.Scopes[^1]);
    }

    [Theory] // the chosen scope comes before anything else, and says where it came from
    [InlineData(InstallScope.User)]
    [InlineData(InstallScope.System)]
    public void ScopeFor_a_chosen_scope_wins_R1_5(InstallScope chosen)
    {
        Assert.Equal((chosen, ScopeSource.Chosen),
            WinGetPackageInstaller.ScopeFor(chosen, keepsInstalled: true, InstallScope.Any, "Machine", InstallScope.Any, ScopeSource.Chosen));
    }

    [Fact]
    public void An_uninstall_passes_no_scope_and_a_choice_beats_a_carried_scope_R1_5()
    {
        var queue = new PackageOperationQueue(new ScriptedInstaller());
        var uninstall = queue.Enqueue(OperationKind.Uninstall, Pandoc with { InstalledVersion = "3.13" }, options: new() { Scope = InstallScope.User });
        var both = queue.Enqueue(OperationKind.Install, Pkg.Remote("B"), options: new() { Scope = InstallScope.User, PreviousScope = InstallScope.System });
        var carried = queue.Enqueue(OperationKind.Install, Pkg.Remote("C"), options: new() { PreviousScope = InstallScope.System });

        Assert.Equal((null, ScopeSource.Installed), PackageOperationQueue.ExplicitScope(uninstall));
        Assert.Equal((InstallScope.User, ScopeSource.Chosen), PackageOperationQueue.ExplicitScope(both));
        Assert.Equal((InstallScope.System, ScopeSource.Installed), PackageOperationQueue.ExplicitScope(carried));
    }

    [Theory]
    [InlineData(InstallScope.System, "Scope: for all users, chosen")]
    [InlineData(InstallScope.User, "Scope: for the current user, chosen")]
    public void The_log_says_it_was_chosen_R1_5(InstallScope scope, string line)
    {
        var queue = new PackageOperationQueue(new ScriptedInstaller
        {
            AutoComplete = new OperationOutcome(OperationResult.Succeeded, "ok") { Scope = scope, ScopeSource = ScopeSource.Chosen },
        });

        var operation = queue.Enqueue(OperationKind.Install, Pandoc, options: new() { Scope = scope });

        Assert.Contains(line, OperationLogStore.Compose(operation, null));
    }
}
