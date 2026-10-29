using CartonStash.Core.Models;
using CartonStash.Core.Services;
using CartonStash.Core.ViewModels;

namespace CartonStash.Core.Tests;

/// <summary>Spec 002.</summary>
public class InstalledViewModelTests
{
    private static (TestApp App, InstalledViewModel Vm) Create(params PackageInfo[] installed)
    {
        var app = new TestApp();
        app.SetInstalled(installed);
        return (app, new InstalledViewModel(app.Store, app.Actions, app.Packages));
    }

    [Fact]
    public async Task Loads_once_per_session() // R1.3
    {
        var (app, vm) = Create(Pkg.Installed("Git.Git", "Git"));

        await vm.LoadAsync();
        await vm.LoadAsync();

        Assert.Equal(1, app.Packages.InstalledCalls);
        Assert.Single(vm.Visible);
    }

    [Fact]
    public async Task Refresh_reloads_with_fresh_catalogs() // R1.3
    {
        var (app, vm) = Create(Pkg.Installed("Git.Git"));
        await vm.LoadAsync();

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(2, app.Packages.InstalledCalls);
        Assert.Equal(1, app.Packages.Invalidations);
    }

    [Fact]
    public async Task Reloads_after_a_successful_operation_once_the_queue_drains() // R1.3, 003 R5.1
    {
        var (app, vm) = Create(Pkg.Installed("Git.Git"));
        await vm.LoadAsync();
        app.Installer.AutoComplete = new OperationOutcome(OperationResult.Succeeded, "ok");

        app.Queue.Enqueue(OperationKind.Uninstall, Pkg.Installed("Git.Git"));
        await app.Queue.Idle;

        Assert.Equal(2, app.Packages.InstalledCalls);
    }

    [Fact]
    public async Task Failed_operation_does_not_reload()
    {
        var (app, vm) = Create(Pkg.Installed("Git.Git"));
        await vm.LoadAsync();
        app.Installer.AutoComplete = new OperationOutcome(OperationResult.Failed, "no");

        app.Queue.Enqueue(OperationKind.Uninstall, Pkg.Installed("Git.Git"));
        await app.Queue.Idle;

        Assert.Equal(1, app.Packages.InstalledCalls);
    }

    [Fact]
    public async Task Sorted_by_name() // R2.3
    {
        var (_, vm) = Create(Pkg.Installed("Z.Z", "zebra"), Pkg.Installed("A.A", "Apple"), Pkg.Installed("M.M", "mango"));

        await vm.LoadAsync();

        Assert.Equal(["Apple", "mango", "zebra"], vm.Visible.Select(r => r.Name));
    }

    [Fact]
    public async Task Filter_matches_name_or_id_case_insensitively_without_querying_again() // R2.1
    {
        var (app, vm) = Create(Pkg.Installed("Git.Git", "Git"), Pkg.Installed("Microsoft.VisualStudioCode", "Visual Studio Code"),
            Pkg.Installed("GitHub.cli", "GitHub CLI"));
        await vm.LoadAsync();

        vm.Filter = "GIT";
        Assert.Equal(["Git", "GitHub CLI"], vm.Visible.Select(r => r.Name));

        vm.Filter = "microsoft.visual";
        Assert.Equal(["Visual Studio Code"], vm.Visible.Select(r => r.Name));

        Assert.Equal(1, app.Packages.InstalledCalls);
    }

    [Fact]
    public async Task Count_shows_shown_of_total() // R2.2
    {
        var (_, vm) = Create(Pkg.Installed("Git.Git", "Git"), Pkg.Installed("7zip.7zip", "7-Zip"), Pkg.Installed("GitHub.cli", "GitHub CLI"));
        await vm.LoadAsync();

        vm.Filter = "git";

        Assert.Equal("2 of 3 packages", vm.CountText);
    }

    [Fact]
    public void Unmanaged_package_says_so() // R3.1
    {
        var unmanaged = Pkg.Installed(@"ARP\Machine\X64\{GUID}", "Contoso Tool", source: "");

        Assert.False(unmanaged.IsManaged);
        Assert.Equal("Not managed by WinGet", unmanaged.SourceText);
        Assert.Equal("winget", Pkg.Installed("Git.Git").SourceText);
    }

    [Fact]
    public async Task Unmanaged_apps_are_hidden_until_asked_for() // R2.4
    {
        var (_, vm) = Create(Pkg.Installed("Git.Git", "Git"), Pkg.Installed("ARP.X", "Contoso", source: ""), Pkg.Installed("ARP.Y", "Game", source: ""));
        await vm.LoadAsync();

        Assert.False(vm.ShowUnmanaged);
        Assert.Equal(["Git"], vm.Visible.Select(r => r.Name));
        Assert.Equal("1 of 3 packages", vm.CountText);

        vm.ShowUnmanaged = true;
        Assert.Equal(["Contoso", "Game", "Git"], vm.Visible.Select(r => r.Name));

        vm.Filter = "game";
        Assert.Equal(["Game"], vm.Visible.Select(r => r.Name));
    }

    [Fact]
    public async Task Load_failure_shows_error_and_retry_recovers() // R5.1
    {
        var (app, vm) = Create();
        app.Packages.Installed = _ => throw new WinGetException("Couldn't reach the package sources.", null);

        await vm.LoadAsync();
        Assert.Equal("Couldn't reach the package sources.", vm.ErrorMessage);
        Assert.False(vm.IsLoading);

        app.SetInstalled(Pkg.Installed("Git.Git"));
        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorMessage);
        Assert.Single(vm.Visible);
    }

    [Fact]
    public async Task Refresh_supersedes_a_running_load()
    {
        var (app, vm) = Create();
        var slow = new TaskCompletionSource<IReadOnlyList<PackageInfo>>();
        app.Packages.Installed = _ => slow.Task;
        var first = vm.LoadAsync();
        Assert.True(vm.IsLoading);

        app.SetInstalled(Pkg.Installed("New.One"));
        await vm.RefreshCommand.ExecuteAsync(null);
        slow.SetResult([Pkg.Installed("Old.One")]);
        await first;

        Assert.Equal(["New.One"], vm.Visible.Select(r => r.Id));
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task Rows_offer_uninstall_only_for_managed_packages() // 003 R2.1
    {
        var (_, vm) = Create(Pkg.Installed("Git.Git", "Git"), Pkg.Installed("ARP.X", "Contoso", source: ""));
        await vm.LoadAsync();
        vm.ShowUnmanaged = true;

        Assert.True(vm.Visible.Single(r => r.Id == "Git.Git").ShowUninstall);
        Assert.False(vm.Visible.Single(r => r.Id == "ARP.X").ShowUninstall);
        Assert.All(vm.Visible, r => Assert.False(r.ShowInstall));
    }
}
