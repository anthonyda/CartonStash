using WinGetFrontend.Core.Models;
using WinGetFrontend.Core.Services;
using WinGetFrontend.Core.ViewModels;

namespace WinGetFrontend.Core.Tests;

public class SearchViewModelTests
{
    private sealed class FakePackageService(Func<string, IReadOnlyList<PackageInfo>> search) : IPackageService
    {
        public Task<IReadOnlyList<PackageInfo>> SearchAsync(string query, int maxResults = 50, CancellationToken cancellationToken = default) =>
            Task.FromResult(search(query));
    }

    private static PackageInfo Pkg(string id) => new(id, id, null, "1.0", "winget");

    [Fact]
    public void Search_is_disabled_for_blank_query()
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
    }

    [Fact]
    public async Task Search_failure_sets_error_and_clears_results()
    {
        var vm = new SearchViewModel(new FakePackageService(_ => throw new InvalidOperationException("source unavailable")))
        {
            Query = "git",
        };

        await vm.SearchCommand.ExecuteAsync(null);

        Assert.Empty(vm.Results);
        Assert.Equal("source unavailable", vm.ErrorMessage);
    }
}
