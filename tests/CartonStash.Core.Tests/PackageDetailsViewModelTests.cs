using CartonStash.Core.Models;
using CartonStash.Core.Services;
using CartonStash.Core.ViewModels;

namespace CartonStash.Core.Tests;

/// <summary>Spec 005.</summary>
public class PackageDetailsViewModelTests
{
    private static (TestApp App, PackageDetailsViewModel Vm) Create()
    {
        var app = new TestApp();
        return (app, new PackageDetailsViewModel(app.Packages));
    }

    private static PackageRowViewModel Row(TestApp app, PackageInfo package) => new(package, app.Actions, RowActions.All);

    [Fact]
    public async Task Shows_loading_then_the_details() // R1.4, R1.2
    {
        var (app, vm) = Create();
        var pending = new TaskCompletionSource<PackageDetails?>();
        app.Packages.Details = (_, _) => pending.Task;

        var load = vm.LoadAsync(Row(app, Pkg.Remote("Git.Git")));
        Assert.True(vm.IsOpen);
        Assert.True(vm.IsLoading);

        pending.SetResult(new PackageDetails { Publisher = "The Git Development Community", HomepageUrl = "https://git-scm.com" });
        await load;

        Assert.Equal(DetailsState.Loaded, vm.State);
        Assert.Equal("The Git Development Community", vm.Details?.Publisher);
        Assert.Equal(new Uri("https://git-scm.com"), vm.HomepageUri);
    }

    [Fact]
    public async Task Selecting_another_row_cancels_the_previous_load() // R1.4 (as 001 R3.2)
    {
        var (app, vm) = Create();
        var slow = new TaskCompletionSource<PackageDetails?>();
        CancellationToken firstToken = default;
        app.Packages.Details = (p, ct) =>
        {
            if (p.Id == "First")
            {
                firstToken = ct;
                return slow.Task;
            }
            return Task.FromResult<PackageDetails?>(new PackageDetails { Publisher = "Second" });
        };

        var first = vm.LoadAsync(Row(app, Pkg.Remote("First")));
        await vm.LoadAsync(Row(app, Pkg.Remote("Second")));
        slow.SetResult(new PackageDetails { Publisher = "First" });
        await first;

        Assert.True(firstToken.IsCancellationRequested);
        Assert.Equal("Second", vm.Details?.Publisher);
        Assert.Equal("Second", vm.Row?.Id);
    }

    [Fact]
    public async Task Unmanaged_package_shows_partial_with_a_note() // R1.5
    {
        var (app, vm) = Create();

        await vm.LoadAsync(Row(app, Pkg.Installed("ARP.X", "Contoso", source: "")));

        Assert.Equal(DetailsState.Partial, vm.State);
        Assert.Contains("wasn't installed through a WinGet source", vm.PartialNote);
    }

    [Fact]
    public async Task Failure_keeps_the_row_and_shows_the_reason() // R1.5
    {
        var (app, vm) = Create();
        app.Packages.Details = (_, _) => throw new WinGetException("Couldn't reach the package sources.", null);

        await vm.LoadAsync(Row(app, Pkg.Remote("A")));

        Assert.Equal(DetailsState.Error, vm.State);
        Assert.Equal("Couldn't reach the package sources.", vm.ErrorMessage);
        Assert.Equal("A", vm.Row?.Id);
    }

    [Fact]
    public async Task Versions_newest_first_with_installed_marked() // R2.1
    {
        var (app, vm) = Create();
        app.Packages.Details = (_, _) => Task.FromResult<PackageDetails?>(new PackageDetails { Versions = ["2.0", "1.0", "0.9"] });

        await vm.LoadAsync(Row(app, Pkg.Installed("A")));

        Assert.Equal("2.0 · 1.0 (installed) · 0.9", vm.VersionsText);
    }

    [Fact]
    public async Task Long_release_notes_are_collapsed_until_show_more() // R2.2
    {
        var (app, vm) = Create();
        var notes = string.Join("\n", Enumerable.Range(1, 30).Select(i => $"line {i}"));
        app.Packages.Details = (_, _) => Task.FromResult<PackageDetails?>(new PackageDetails { ReleaseNotes = notes });

        await vm.LoadAsync(Row(app, Pkg.Remote("A")));

        Assert.True(vm.ReleaseNotesIsLong);
        Assert.Equal(PackageDetailsViewModel.CollapsedReleaseNoteLines, vm.ReleaseNotesMaxLines);
        vm.ToggleReleaseNotesCommand.Execute(null);
        Assert.Equal(0, vm.ReleaseNotesMaxLines);
    }

    [Fact]
    public async Task Close_hides_the_pane() // R4.1
    {
        var (app, vm) = Create();
        await vm.LoadAsync(Row(app, Pkg.Remote("A")));

        vm.CloseCommand.Execute(null);

        Assert.False(vm.IsOpen);
    }

    [Theory] // security: only web links are launched
    [InlineData("https://git-scm.com", true)]
    [InlineData("http://example.com/x", true)]
    [InlineData("file:///C:/Windows/System32/cmd.exe", false)]
    [InlineData("ms-settings:privacy", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("not a url", false)]
    [InlineData(null, false)]
    public void Only_http_links_are_offered(string? url, bool allowed)
    {
        Assert.Equal(allowed, PackageDetailsViewModel.SafeUri(url) is not null);
    }
}
