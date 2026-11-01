using System.ComponentModel;
using CartonStash.Core.Models;
using CartonStash.Core.Services;
using CartonStash.Core.ViewModels;

namespace CartonStash.Core.Tests;

/// <summary>
/// 011 T8: x:Bind doesn't call a function again once its argument is null, so views bind these bools. Each must fall with
/// its message and raise PropertyChanged, or the message stays on screen with no text.
/// </summary>
public class MessageFlagTests
{
    private static List<string?> Watch(INotifyPropertyChanged source)
    {
        var changed = new List<string?>();
        source.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        return changed;
    }

    private static PackageRowViewModel Row(TestApp app, string id) => new(Pkg.Remote(id), app.Actions, RowActions.All);

    [Fact]
    public async Task Search_error_closes_after_a_search_that_works()
    {
        var app = new TestApp();
        app.Packages.Search = (_, _) => throw new WinGetException("Couldn't search.", null);
        var vm = new SearchViewModel(app.Packages, app.Actions, app.Agreements) { Query = "git" };
        await vm.SearchCommand.ExecuteAsync(null);
        Assert.True(vm.HasError);

        app.Packages.Search = (_, _) => Task.FromResult<IReadOnlyList<PackageInfo>>([Pkg.Remote("Git.Git")]);
        var changed = Watch(vm);
        await vm.SearchCommand.ExecuteAsync(null);

        Assert.False(vm.HasError);
        Assert.Contains(nameof(vm.HasError), changed);
    }

    [Fact]
    public async Task Empty_message_and_source_notice_clear_on_the_next_search()
    {
        var app = new TestApp();
        app.Packages.Search = (_, _) => Task.FromResult<IReadOnlyList<PackageInfo>>([]);
        app.Packages.SourceFailures = [new SourceFailure("msstore", "'msstore' can't be reached.", 0)];
        var vm = new SearchViewModel(app.Packages, app.Actions, app.Agreements) { Query = "nothing" };
        await vm.SearchCommand.ExecuteAsync(null);
        Assert.True(vm.HasEmptyMessage);
        Assert.True(vm.HasSourceNotice);

        app.Packages.Search = (_, _) => Task.FromResult<IReadOnlyList<PackageInfo>>([Pkg.Remote("Git.Git")]);
        app.Packages.SourceFailures = [];
        var changed = Watch(vm);
        await vm.SearchCommand.ExecuteAsync(null);

        Assert.False(vm.HasEmptyMessage);
        Assert.False(vm.HasSourceNotice);
        Assert.Contains(nameof(vm.HasEmptyMessage), changed);
        Assert.Contains(nameof(vm.HasSourceNotice), changed);
    }

    [Fact]
    public async Task Installed_error_closes_after_a_retry_that_works()
    {
        var app = new TestApp();
        app.Packages.Installed = _ => throw new WinGetException("Couldn't list packages.", null);
        var vm = new InstalledViewModel(app.Store, app.Actions, app.Packages);
        await vm.LoadAsync();
        Assert.True(vm.HasError);

        app.SetInstalled(Pkg.Installed("Git.Git"));
        var changed = Watch(vm);
        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.False(vm.HasError);
        Assert.Contains(nameof(vm.HasError), changed);
    }

    [Fact]
    public async Task Updates_error_closes_after_a_retry_that_works()
    {
        var app = new TestApp();
        app.Packages.Installed = _ => throw new WinGetException("Couldn't list packages.", null);
        var vm = new UpdatesViewModel(app.Store, app.Actions, app.Packages, app.Settings);
        await vm.LoadAsync();
        Assert.True(vm.HasError);

        app.SetInstalled(Pkg.Installed("Git.Git"));
        var changed = Watch(vm);
        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.False(vm.HasError);
        Assert.Contains(nameof(vm.HasError), changed);
    }

    [Fact]
    public async Task Sources_error_and_status_clear()
    {
        var packages = new FakePackageService();
        packages.Sources.Add(new SourceInfo("winget", "winget", "", "", true, false, null, []));
        packages.RefreshErrors["winget"] = "Couldn't reach the source.";
        var queue = new PackageOperationQueue(new ScriptedInstaller());
        var agreements = new SourceAgreementCheck(packages, new FakeDialogs(), new InstalledPackagesStore(packages, queue));
        var vm = new SettingsViewModel(new FakeSettings(), packages, agreements, new FakeClipboard());
        await vm.LoadCommand.ExecuteAsync(null);
        await vm.RefreshSourcesCommand.ExecuteAsync(null);
        Assert.True(vm.HasSourcesError);
        Assert.False(vm.HasSourcesStatus);

        packages.RefreshErrors.Clear();
        var changed = Watch(vm);
        await vm.RefreshSourcesCommand.ExecuteAsync(null);

        Assert.False(vm.HasSourcesError);
        Assert.True(vm.HasSourcesStatus);
        Assert.Contains(nameof(vm.HasSourcesError), changed);

        changed.Clear();
        vm.CopyDiagnosticsCommand.Execute(null);

        Assert.False(vm.HasSourcesStatus);
        Assert.Contains(nameof(vm.HasSourcesStatus), changed);
    }

    [Fact]
    public void Setup_status_clears_once_winget_opens()
    {
        var installed = false;
        var vm = new WinGetSetupViewModel(false, () => installed, () => true);
        vm.CheckAgainCommand.Execute(null);
        Assert.True(vm.HasStatus);

        installed = true;
        var changed = Watch(vm);
        vm.CheckAgainCommand.Execute(null);

        Assert.False(vm.HasStatus);
        Assert.Contains(nameof(vm.HasStatus), changed);
    }

    [Fact]
    public void Operation_message_clears_on_retry()
    {
        var app = new TestApp();
        var op = app.Queue.Enqueue(OperationKind.Install, Pkg.Remote("A"));
        app.Installer.Complete("A", OperationResult.Failed, "The installer failed.");
        Assert.True(op.HasMessage);

        var changed = Watch(op);
        op.RetryCommand.Execute(null);

        Assert.False(op.HasMessage);
        Assert.Contains(nameof(op.HasMessage), changed);
    }

    [Fact]
    public async Task Details_error_and_partial_note_clear_on_the_next_package()
    {
        var app = new TestApp();
        var vm = new PackageDetailsViewModel(app.Packages);
        app.Packages.Details = (_, _) => throw new WinGetException("Couldn't load.", null);
        await vm.LoadAsync(Row(app, "A"));
        Assert.True(vm.HasError);

        app.Packages.Details = (_, _) => Task.FromResult<PackageDetails?>(null);
        await vm.LoadAsync(Row(app, "B"));
        Assert.False(vm.HasError);
        Assert.True(vm.HasPartialNote);

        app.Packages.Details = (_, _) => Task.FromResult<PackageDetails?>(new PackageDetails());
        await vm.LoadAsync(Row(app, "C"));
        Assert.False(vm.HasPartialNote);
    }

    [Fact]
    public async Task Details_sections_leave_with_the_package_that_had_them()
    {
        var app = new TestApp();
        var vm = new PackageDetailsViewModel(app.Packages);
        app.Packages.Details = (_, _) => Task.FromResult<PackageDetails?>(new PackageDetails
        {
            Publisher = "P",
            ShortDescription = "S",
            Description = "D",
            License = "MIT",
            LicenseUrl = "https://example.com/l",
            HomepageUrl = "https://example.com/h",
            PublisherUrl = "https://example.com/p",
            SupportUrl = "https://example.com/s",
            PrivacyUrl = "https://example.com/y",
            ReleaseNotesUrl = "https://example.com/r",
            Tags = ["t"],
            Versions = ["1.0"],
        });
        await vm.LoadAsync(Row(app, "Full"));

        string[] flags =
        [
            nameof(vm.HasPublisher), nameof(vm.HasShortDescription), nameof(vm.HasDescription), nameof(vm.HasLicense),
            nameof(vm.HasLicenseUri), nameof(vm.HasHomepageUri), nameof(vm.HasPublisherUri), nameof(vm.HasSupportUri),
            nameof(vm.HasPrivacyUri), nameof(vm.HasReleaseNotesUri), nameof(vm.HasTags), nameof(vm.HasVersions),
        ];
        bool Flag(string name) => (bool)typeof(PackageDetailsViewModel).GetProperty(name)!.GetValue(vm)!;
        Assert.All(flags, name => Assert.True(Flag(name), name));

        app.Packages.Details = (_, _) => Task.FromResult<PackageDetails?>(new PackageDetails { Publisher = "" });
        var changed = Watch(vm);
        await vm.LoadAsync(Row(app, "Bare"));

        Assert.All(flags, name => Assert.False(Flag(name), name));
        Assert.All(flags, name => Assert.Contains(name, changed));
    }
}
