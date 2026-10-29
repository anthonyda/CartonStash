using WinGetFrontend.Core.Models;
using WinGetFrontend.Core.Services;
using WinGetFrontend.Core.ViewModels;

namespace WinGetFrontend.Core.Tests;

public class SearchViewModelTests
{
    private sealed class FakePackageService(Func<string, CancellationToken, Task<IReadOnlyList<PackageInfo>>> search) : IPackageService
    {
        public FakePackageService(Func<string, IReadOnlyList<PackageInfo>> search)
            : this((q, _) => Task.FromResult(search(q)))
        {
        }

        public Task<IReadOnlyList<PackageInfo>> SearchAsync(string query, int maxResults = 50, CancellationToken cancellationToken = default) =>
            search(query, cancellationToken);
    }

    private static PackageInfo Pkg(string id) => new(id, id, null, "1.0", "winget");

    [Fact]
    public void Search_is_disabled_for_blank_query() // R1.2
    {
        var vm = new SearchViewModel(new FakePackageService(_ => []));

        vm.Query = "   ";

        Assert.False(vm.SearchCommand.CanExecute(null));
    }

    [Fact]
    public async Task Search_populates_results()
    {
        var vm = new SearchViewModel(new FakePackageService(_ => [Pkg("Git.Git"), Pkg("GitHub.cli")]))
        {
            Query = "git",
        };

        await vm.SearchCommand.ExecuteAsync(null);

        Assert.Equal(["Git.Git", "GitHub.cli"], vm.Results.Select(r => r.Id));
        Assert.Null(vm.ErrorMessage);
        Assert.Null(vm.EmptyMessage);
        Assert.False(vm.IsSearching);
    }

    [Fact]
    public async Task New_search_supersedes_running_one() // R3.2
    {
        var slow = new TaskCompletionSource<IReadOnlyList<PackageInfo>>();
        CancellationToken firstToken = default;
        var vm = new SearchViewModel(new FakePackageService((q, ct) =>
        {
            if (q == "first")
            {
                firstToken = ct;
                return slow.Task; // ignores cancellation on purpose: results must still be dropped
            }
            return Task.FromResult<IReadOnlyList<PackageInfo>>([Pkg("Second.Result")]);
        }));

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
        var vm = new SearchViewModel(new FakePackageService(_ => []))
        {
            Query = "  nothing-matches ",
        };

        await vm.SearchCommand.ExecuteAsync(null);

        Assert.Empty(vm.Results);
        Assert.Equal("No packages found for 'nothing-matches'", vm.EmptyMessage);
    }

    [Fact]
    public async Task WinGet_failure_shows_its_message_and_keeps_query() // R3.4
    {
        var vm = new SearchViewModel(new FakePackageService(_ => throw new WinGetException("Couldn't reach the package sources.", null)))
        {
            Query = "git",
        };

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
        var vm = new SearchViewModel(new FakePackageService(_ => throw new InvalidOperationException("boom")))
        {
            Query = "git",
        };

        await vm.SearchCommand.ExecuteAsync(null);

        Assert.Equal("The search failed unexpectedly: boom", vm.ErrorMessage);
    }

    [Fact]
    public async Task New_search_clears_previous_error() // R3.4
    {
        var fail = true;
        var vm = new SearchViewModel(new FakePackageService(_ => fail ? throw new WinGetException("down", null) : [Pkg("Git.Git")]))
        {
            Query = "git",
        };
        await vm.SearchCommand.ExecuteAsync(null);

        fail = false;
        await vm.SearchCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorMessage);
        Assert.Single(vm.Results);
    }
}
