using CartonStash.Core.Models;
using CartonStash.Core.Services;
using CartonStash.Core.ViewModels;

namespace CartonStash.Core.Tests;

/// <summary>BR-14: a load that a newer one replaced may fail for a real reason; that error goes nowhere.</summary>
public class SupersededLoadTests
{
    [Fact]
    public async Task A_superseded_installed_load_that_fails_doesnt_throw_002_R1_3()
    {
        var app = new TestApp();
        var first = new TaskCompletionSource<IReadOnlyList<PackageInfo>>();
        var calls = 0;
        app.Packages.Installed = _ => ++calls == 1 ? first.Task : Task.FromResult<IReadOnlyList<PackageInfo>>([Pkg.Installed("A")]);
        var old = app.Store.ReloadAsync();
        await app.Store.ReloadAsync(); // Refresh pressed again

        first.SetException(new WinGetException("A source failed.", null)); // not a cancellation
        await old; // doesn't throw

        Assert.Null(app.Store.ErrorMessage);
        Assert.Equal(["A"], app.Store.Packages.Select(p => p.Id));
    }

    [Fact]
    public async Task A_superseded_search_that_fails_doesnt_throw_001_R3_2()
    {
        var app = new TestApp();
        var first = new TaskCompletionSource<IReadOnlyList<PackageInfo>>();
        app.Packages.Search = (q, _) => q == "a" ? first.Task : Task.FromResult<IReadOnlyList<PackageInfo>>([Pkg.Remote("B")]);
        var vm = new SearchViewModel(app.Packages, app.Actions, app.Agreements, app.ReleaseNotes);
        vm.Query = "a";
        var old = vm.SearchCommand.ExecuteAsync(null);
        vm.Query = "b";
        await vm.SearchCommand.ExecuteAsync(null);

        first.SetException(new WinGetException("A source failed.", null));
        await old;

        Assert.Null(vm.ErrorMessage);
        Assert.Equal(["B"], vm.Results.Select(r => r.Id));
    }
}
