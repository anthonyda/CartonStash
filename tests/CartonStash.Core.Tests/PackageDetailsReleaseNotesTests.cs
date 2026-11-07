using System.Globalization;
using CartonStash.Core.Models;
using CartonStash.Core.Services;
using CartonStash.Core.ViewModels;

namespace CartonStash.Core.Tests;

/// <summary>Spec 012: release notes in the details pane.</summary>
public class PackageDetailsReleaseNotesTests
{
    private const string BatUrl = "https://github.com/sharkdp/bat/releases/tag/v0.26.1";
    private static readonly GitHubRelease Bat = new("sharkdp", "bat", "v0.26.1");

    private static readonly GitHubNotes BatNotes = new("v0.26.1", new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero),
        new Uri(BatUrl), "## Fixes\n- Paging");

    private static (TestApp App, PackageDetailsViewModel Vm) Create(string? notes = "Fixes\n- WinGet's own", string? url = BatUrl)
    {
        var app = new TestApp();
        app.Packages.Details = (_, _) => Task.FromResult<PackageDetails?>(new PackageDetails { ReleaseNotes = notes, ReleaseNotesUrl = url });
        return (app, new PackageDetailsViewModel(app.Packages, app.ReleaseNotes));
    }

    private static PackageRowViewModel Row(TestApp app, string id) => new(Pkg.Remote(id), app.Actions, RowActions.All);

    private static string Notes(PackageDetailsViewModel vm) => NotesDump.Describe(vm.ReleaseNotes);

    [Fact]
    public async Task WinGets_text_is_formatted() // R1
    {
        var (app, vm) = Create(url: null);
        await vm.LoadAsync(Row(app, "A"));

        Assert.True(vm.HasReleaseNotes);
        Assert.Equal("P: **Fixes**\n- WinGet's own", Notes(vm));
        Assert.False(vm.CanShowGitHubNotes);
        Assert.Null(vm.GitHubRelease);
    }

    [Fact]
    public async Task Opening_a_package_doesnt_contact_GitHub() // R2.1, R2.2
    {
        var (app, vm) = Create();
        await vm.LoadAsync(Row(app, "A"));

        Assert.Equal(Bat, vm.GitHubRelease);
        Assert.True(vm.CanShowGitHubNotes);
        Assert.Empty(app.ReleaseNotes.Requests);
    }

    [Fact]
    public async Task A_package_with_only_a_GitHub_link_shows_the_section() // R2.1
    {
        var (app, vm) = Create(notes: null);
        await vm.LoadAsync(Row(app, "A"));

        Assert.False(vm.HasReleaseNotes);
        Assert.True(vm.ShowReleaseNotesSection);
        Assert.True(vm.CanShowGitHubNotes);
    }

    [Fact]
    public async Task A_link_elsewhere_offers_no_button() // R2.1
    {
        var (app, vm) = Create(notes: null, url: "https://code.visualstudio.com/updates/v1_143");
        await vm.LoadAsync(Row(app, "A"));

        Assert.False(vm.CanShowGitHubNotes);
        Assert.False(vm.ShowReleaseNotesSection);
        Assert.True(vm.HasReleaseNotesUri);
    }

    [Fact]
    public async Task The_button_loads_and_shows_GitHubs_notes() // R2.3, R2.4
    {
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            var (app, vm) = Create();
            app.ReleaseNotes.Get = (_, _) => Task.FromResult(GitHubNotesResult.Loaded(BatNotes));
            await vm.LoadAsync(Row(app, "A"));

            await vm.LoadGitHubNotesCommand.ExecuteAsync(null);

            Assert.Equal(Bat, Assert.Single(app.ReleaseNotes.Requests).Release);
            Assert.Equal("H2: Fixes\n- Paging", Notes(vm));
            Assert.True(vm.NotesFromGitHub);
            Assert.Equal("v0.26.1 · 1 August 2026 · from GitHub", vm.GitHubNotesHeader);
            Assert.False(vm.CanShowGitHubNotes);
            Assert.False(vm.IsLoadingGitHubNotes);
            Assert.False(vm.HasGitHubNotesMessage);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public async Task Shows_loading_while_GitHub_answers() // R2.3
    {
        var (app, vm) = Create();
        var answer = new TaskCompletionSource<GitHubNotesResult>();
        app.ReleaseNotes.Get = (_, _) => answer.Task;
        await vm.LoadAsync(Row(app, "A"));

        var press = vm.LoadGitHubNotesCommand.ExecuteAsync(null);
        Assert.True(vm.IsLoadingGitHubNotes);
        Assert.False(vm.CanShowGitHubNotes);
        Assert.True(vm.ShowReleaseNotesSection);

        answer.SetResult(GitHubNotesResult.Loaded(BatNotes));
        await press;
        Assert.False(vm.IsLoadingGitHubNotes);
    }

    [Fact]
    public async Task Reopening_shows_cached_notes_with_no_request() // R2.9
    {
        var (app, vm) = Create();
        app.ReleaseNotes.Cache[Bat] = BatNotes;

        await vm.LoadAsync(Row(app, "A"));

        Assert.Empty(app.ReleaseNotes.Requests);
        Assert.True(vm.NotesFromGitHub);
        Assert.Equal("H2: Fixes\n- Paging", Notes(vm));
    }

    [Fact]
    public async Task Another_package_cancels_the_request_and_drops_its_answer() // R2.10
    {
        var (app, vm) = Create();
        var answer = new TaskCompletionSource<GitHubNotesResult>();
        app.ReleaseNotes.Get = (_, _) => answer.Task;
        await vm.LoadAsync(Row(app, "A"));
        var press = vm.LoadGitHubNotesCommand.ExecuteAsync(null);

        await vm.LoadAsync(Row(app, "B"));
        Assert.True(app.ReleaseNotes.Requests[0].Token.IsCancellationRequested);
        answer.SetResult(GitHubNotesResult.Loaded(BatNotes));
        await press;

        Assert.False(vm.NotesFromGitHub);
        Assert.Equal("P: **Fixes**\n- WinGet's own", Notes(vm));
        Assert.False(vm.IsLoadingGitHubNotes);
        Assert.True(vm.CanShowGitHubNotes);
    }

    [Fact]
    public async Task Closing_the_pane_cancels_the_request() // R2.10
    {
        var (app, vm) = Create();
        app.ReleaseNotes.Get = async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return GitHubNotesResult.Loaded(BatNotes);
        };
        await vm.LoadAsync(Row(app, "A"));
        var press = vm.LoadGitHubNotesCommand.ExecuteAsync(null);

        vm.CloseCommand.Execute(null);
        await press;

        Assert.True(app.ReleaseNotes.Requests[0].Token.IsCancellationRequested);
        Assert.False(vm.IsLoadingGitHubNotes);
        Assert.False(vm.HasGitHubNotesMessage);
    }

    [Theory] // R2.11
    [InlineData(GitHubNotesOutcome.Unreachable, "Couldn't reach GitHub.", true)]
    [InlineData(GitHubNotesOutcome.NotFound, "GitHub has no release with this tag.", false)]
    [InlineData(GitHubNotesOutcome.Failed, "Couldn't load the notes from GitHub.", true)]
    public async Task Failures_say_what_happened_and_keep_WinGets_notes(GitHubNotesOutcome outcome, string message, bool retry)
    {
        var (app, vm) = Create();
        app.ReleaseNotes.Get = (_, _) => Task.FromResult(new GitHubNotesResult(outcome));
        await vm.LoadAsync(Row(app, "A"));

        await vm.LoadGitHubNotesCommand.ExecuteAsync(null);

        Assert.Equal(message, vm.GitHubNotesMessage);
        Assert.True(vm.HasGitHubNotesMessage);
        Assert.Equal(retry, vm.CanRetryGitHubNotes);
        Assert.Equal("P: **Fixes**\n- WinGet's own", Notes(vm));
        Assert.False(vm.NotesFromGitHub);
    }

    [Fact]
    public async Task Rate_limit_says_when_to_try_again() // R2.11
    {
        var at = new DateTimeOffset(2026, 11, 7, 13, 32, 0, TimeSpan.Zero);
        var (app, vm) = Create();
        app.ReleaseNotes.Get = (_, _) => Task.FromResult(new GitHubNotesResult(GitHubNotesOutcome.RateLimited, RetryAt: at));
        await vm.LoadAsync(Row(app, "A"));

        await vm.LoadGitHubNotesCommand.ExecuteAsync(null);

        Assert.Equal($"GitHub limits requests without sign-in. Try again after {at.ToLocalTime().ToString("t", CultureInfo.CurrentCulture)}.", vm.GitHubNotesMessage);
        Assert.False(vm.CanRetryGitHubNotes);
    }

    [Fact]
    public async Task An_unexpected_exception_is_a_failure() // R2.11
    {
        var (app, vm) = Create();
        app.ReleaseNotes.Get = (_, _) => throw new InvalidOperationException("boom");
        await vm.LoadAsync(Row(app, "A"));

        await vm.LoadGitHubNotesCommand.ExecuteAsync(null);

        Assert.Equal("Couldn't load the notes from GitHub.", vm.GitHubNotesMessage);
        Assert.True(vm.CanRetryGitHubNotes);
    }

    [Fact]
    public async Task Retry_asks_again() // R2.11
    {
        var (app, vm) = Create();
        var calls = 0;
        app.ReleaseNotes.Get = (_, _) => Task.FromResult(++calls == 1
            ? new GitHubNotesResult(GitHubNotesOutcome.Unreachable)
            : GitHubNotesResult.Loaded(BatNotes));
        await vm.LoadAsync(Row(app, "A"));

        await vm.LoadGitHubNotesCommand.ExecuteAsync(null);
        await vm.LoadGitHubNotesCommand.ExecuteAsync(null);

        Assert.Equal(2, app.ReleaseNotes.Requests.Count);
        Assert.True(vm.NotesFromGitHub);
        Assert.False(vm.HasGitHubNotesMessage);
    }

    [Fact]
    public async Task A_release_with_no_notes_says_so_and_keeps_WinGets() // R2.4
    {
        var (app, vm) = Create();
        app.ReleaseNotes.Get = (_, _) => Task.FromResult(GitHubNotesResult.Loaded(BatNotes with { Body = null }));
        await vm.LoadAsync(Row(app, "A"));

        await vm.LoadGitHubNotesCommand.ExecuteAsync(null);

        Assert.Equal("This GitHub release has no notes.", vm.GitHubNotesMessage);
        Assert.False(vm.CanRetryGitHubNotes);
        Assert.False(vm.CanShowGitHubNotes);
        Assert.False(vm.NotesFromGitHub);
        Assert.Equal("P: **Fixes**\n- WinGet's own", Notes(vm));
    }

    [Fact]
    public async Task The_next_package_starts_clean() // R2.11
    {
        var (app, vm) = Create();
        app.ReleaseNotes.Get = (_, _) => Task.FromResult(new GitHubNotesResult(GitHubNotesOutcome.Unreachable));
        await vm.LoadAsync(Row(app, "A"));
        await vm.LoadGitHubNotesCommand.ExecuteAsync(null);
        Assert.True(vm.HasGitHubNotesMessage);

        app.Packages.Details = (_, _) => Task.FromResult<PackageDetails?>(new PackageDetails());
        await vm.LoadAsync(Row(app, "B"));

        Assert.False(vm.HasGitHubNotesMessage);
        Assert.False(vm.CanRetryGitHubNotes);
        Assert.False(vm.HasReleaseNotes);
        Assert.False(vm.CanShowGitHubNotes);
        Assert.False(vm.ShowReleaseNotesSection);
        Assert.Null(vm.GitHubNotesHeader);
    }
}
