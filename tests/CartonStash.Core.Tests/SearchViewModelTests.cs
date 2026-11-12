using CartonStash.Core.Models;
using CartonStash.Core.Services;
using CartonStash.Core.ViewModels;

namespace CartonStash.Core.Tests;

public class SearchViewModelTests
{
    private static SearchViewModel Create(Func<string, CancellationToken, Task<IReadOnlyList<PackageInfo>>> search)
    {
        var app = new TestApp();
        app.Packages.Search = search;
        return new SearchViewModel(app.Packages, app.Actions, app.Agreements, app.ReleaseNotes);
    }

    private static SearchViewModel Create(Func<string, IReadOnlyList<PackageInfo>> search) =>
        Create((q, _) => Task.FromResult(search(q)));

    private static PackageInfo Pkg(string id) => new(id, id, null, "1.0", "winget");

    [Fact]
    public void Search_is_disabled_for_blank_query() // R1.2
    {
        var vm = Create(_ => []);

        vm.Query = "   ";

        Assert.False(vm.SearchCommand.CanExecute(null));
    }

    [Fact]
    public async Task New_search_removes_old_results_without_a_reset() // R4.1: a reset crashed Tab into the results (E_BOUNDS)
    {
        var vm = Create(q => q == "many" ? [.. Enumerable.Range(0, 20).Select(i => Pkg($"P{i}"))] : [Pkg("jqlang.jq")]);
        vm.Query = "many";
        await vm.SearchCommand.ExecuteAsync(null);
        var changes = new List<System.Collections.Specialized.NotifyCollectionChangedAction>();
        vm.Results.CollectionChanged += (_, e) => changes.Add(e.Action);

        vm.Query = "jq";
        await vm.SearchCommand.ExecuteAsync(null);

        Assert.Equal(["jqlang.jq"], vm.Results.Select(r => r.Id));
        Assert.DoesNotContain(System.Collections.Specialized.NotifyCollectionChangedAction.Reset, changes);
    }

    [Fact]
    public async Task Search_populates_results()
    {
        var vm = Create(_ => [Pkg("Git.Git"), Pkg("GitHub.cli")]);
        vm.Query = "git";

        await vm.SearchCommand.ExecuteAsync(null);

        Assert.Equal(["Git.Git", "GitHub.cli"], vm.Results.Select(r => r.Id));
        Assert.Null(vm.ErrorMessage);
        Assert.Null(vm.EmptyMessage);
        Assert.False(vm.IsSearching);
    }

    [Fact] // 011 R2.1
    public async Task Results_stay_usable_and_the_notice_names_a_missing_source()
    {
        var app = new TestApp();
        app.Packages.Search = (_, _) => Task.FromResult<IReadOnlyList<PackageInfo>>([Pkg("Git.Git")]);
        app.Packages.SourceFailures = [new SourceFailure("msstore", "'msstore' can't be reached.", 0)];
        var vm = new SearchViewModel(app.Packages, app.Actions, app.Agreements, app.ReleaseNotes);
        vm.Query = "git";

        await vm.SearchCommand.ExecuteAsync(null);

        Assert.Equal("Results don't include: 'msstore' can't be reached.", vm.SourceNotice);
        Assert.Single(vm.Results);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact] // 011 R1.5: the notice follows the latest search
    public async Task The_next_search_clears_the_notice()
    {
        var app = new TestApp();
        app.Packages.SourceFailures = [new SourceFailure("msstore", "'msstore' failed.", 0)];
        var vm = new SearchViewModel(app.Packages, app.Actions, app.Agreements, app.ReleaseNotes);
        vm.Query = "git";
        await vm.SearchCommand.ExecuteAsync(null);

        app.Packages.SourceFailures = [];
        await vm.SearchCommand.ExecuteAsync(null);

        Assert.Null(vm.SourceNotice);
    }

    [Fact] // 011 R2.5: not "no results"
    public async Task When_every_source_fails_the_error_is_shown()
    {
        var vm = Create((_, _) => throw new WinGetException("'msstore' failed. 'winget' failed.", 0));
        vm.Query = "git";

        await vm.SearchCommand.ExecuteAsync(null);

        Assert.Equal("'msstore' failed. 'winget' failed.", vm.ErrorMessage);
        Assert.Null(vm.EmptyMessage);
    }

    [Fact]
    public async Task New_search_supersedes_running_one() // R3.2
    {
        var slow = new TaskCompletionSource<IReadOnlyList<PackageInfo>>();
        CancellationToken firstToken = default;
        var vm = Create((q, ct) =>
        {
            if (q == "first")
            {
                firstToken = ct;
                return slow.Task; // ignores cancellation on purpose: results must still be dropped
            }
            return Task.FromResult<IReadOnlyList<PackageInfo>>([Pkg("Second.Result")]);
        });

        vm.Query = "first";
        var first = vm.SearchCommand.ExecuteAsync(null);
        Assert.True(vm.IsSearching);

        vm.Query = "second";
        await vm.SearchCommand.ExecuteAsync(null);
        slow.SetResult([Pkg("First.Result")]);
        await first;

        Assert.True(firstToken.IsCancellationRequested);
        Assert.Equal(["Second.Result"], vm.Results.Select(r => r.Id));
        Assert.False(vm.IsSearching);
    }

    [Fact]
    public async Task No_matches_shows_empty_message() // R3.3
    {
        var vm = Create(_ => []);
        vm.Query = "  nothing-matches ";

        await vm.SearchCommand.ExecuteAsync(null);

        Assert.Empty(vm.Results);
        Assert.Equal("No packages found for 'nothing-matches'", vm.EmptyMessage);
    }

    [Fact]
    public async Task WinGet_failure_shows_its_message_and_keeps_query() // R3.4
    {
        var vm = Create(_ => throw new WinGetException("Couldn't reach the package sources.", null));
        vm.Query = "git";

        await vm.SearchCommand.ExecuteAsync(null);

        Assert.Empty(vm.Results);
        Assert.Equal("Couldn't reach the package sources.", vm.ErrorMessage);
        Assert.Equal("git", vm.Query);
        Assert.Null(vm.EmptyMessage);
        Assert.False(vm.IsSearching);
    }

    [Fact]
    public async Task Unexpected_failure_gets_readable_prefix() // R3.4
    {
        var vm = Create(_ => throw new InvalidOperationException("boom"));
        vm.Query = "git";

        await vm.SearchCommand.ExecuteAsync(null);

        Assert.Equal("The search failed unexpectedly: boom", vm.ErrorMessage);
    }

    [Fact]
    public async Task New_search_clears_previous_error() // R3.4
    {
        var fail = true;
        var vm = Create(_ => fail ? throw new WinGetException("down", null) : [Pkg("Git.Git")]);
        vm.Query = "git";
        await vm.SearchCommand.ExecuteAsync(null);

        fail = false;
        await vm.SearchCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorMessage);
        Assert.Single(vm.Results);
    }
    [Fact] // BR-03: a new search makes new rows; the open pane follows the new row, so the pane's actions reach the list (005 R3.1)
    public async Task The_open_pane_follows_the_new_row_after_a_new_search_005_R3_1()
    {
        var vm = Create(_ => [Pkg("Spotify.Spotify"), Pkg("Other")]);
        vm.Query = "spotify";
        await vm.SearchCommand.ExecuteAsync(null);
        await vm.Details.LoadAsync(vm.Results[0]);

        await vm.SearchCommand.ExecuteAsync(null);

        Assert.True(vm.Details.IsOpen);
        Assert.Same(vm.Results[0], vm.Details.Row);
    }

    [Fact] // BR-03: the package is no longer listed: the pane closes
    public async Task The_open_pane_closes_when_a_new_search_doesnt_list_its_package_005_R4_1()
    {
        var vm = Create(q => q == "a" ? [Pkg("A")] : [Pkg("B")]);
        vm.Query = "a";
        await vm.SearchCommand.ExecuteAsync(null);
        await vm.Details.LoadAsync(vm.Results[0]);

        vm.Query = "b";
        await vm.SearchCommand.ExecuteAsync(null);

        Assert.False(vm.Details.IsOpen);
    }
}
