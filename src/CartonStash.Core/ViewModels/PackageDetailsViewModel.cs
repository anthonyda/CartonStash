using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CartonStash.Core.Models;
using CartonStash.Core.Services;

namespace CartonStash.Core.ViewModels;

public enum DetailsState
{
    Loading,
    Loaded,

    /// <summary>Only what the row knows: unmanaged package, or WinGet has no metadata (005 R1.5).</summary>
    Partial,
    Error,
}

/// <summary>The details pane (spec 005). Each list page owns one. Release notes: spec 012.</summary>
public sealed partial class PackageDetailsViewModel(IPackageService packages, IReleaseNotesService releaseNotes) : ObservableObject
{
    private CancellationTokenSource? _currentLoad;

    /// <summary>The GitHub request; cancelled by another package or by closing the pane (012 R2.10).</summary>
    private CancellationTokenSource? _notesLoad;

    /// <summary>
    /// Formatted release notes: WinGet's text (012 R1), replaced by GitHub's notes once loaded (R2.4).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReleaseNotes), nameof(ShowReleaseNotesSection))]
    public partial NotesDocument? ReleaseNotes { get; private set; }

    /// <summary>Set when the release-notes URL is a GitHub release page (012 R2.1).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanShowGitHubNotes), nameof(ShowReleaseNotesSection))]
    public partial GitHubRelease? GitHubRelease { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanShowGitHubNotes), nameof(ShowReleaseNotesSection), nameof(GitHubNotesStatus), nameof(HasGitHubNotesStatus))]
    public partial bool IsLoadingGitHubNotes { get; private set; }

    /// <summary>GitHub answered, with or without notes: the button has done its job.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanShowGitHubNotes), nameof(ShowReleaseNotesSection))]
    public partial bool GitHubNotesLoaded { get; private set; }

    /// <summary>What went wrong asking GitHub, or that the release has no notes (012 R2.11).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGitHubNotesMessage), nameof(ShowReleaseNotesSection), nameof(GitHubNotesStatus), nameof(HasGitHubNotesStatus))]
    public partial string? GitHubNotesMessage { get; private set; }

    /// <summary>Only when trying again can help: no connection, or a failure (012 R2.11).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanShowGitHubNotes), nameof(ShowReleaseNotesSection))]
    public partial bool CanRetryGitHubNotes { get; private set; }

    /// <summary>"{title} · {date} · from GitHub" while GitHub's notes are shown (012 R2.4).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NotesFromGitHub))]
    public partial string? GitHubNotesHeader { get; private set; }

    [ObservableProperty]
    public partial bool IsOpen { get; private set; }

    /// <summary>The row the pane shows; its actions and progress are the row's (005 R3.1).</summary>
    [ObservableProperty]
    public partial PackageRowViewModel? Row { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoading), nameof(PartialNote), nameof(HasPartialNote))]
    public partial DetailsState State { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HomepageUri), nameof(PublisherUri), nameof(SupportUri), nameof(PrivacyUri),
        nameof(LicenseUri), nameof(ReleaseNotesUri), nameof(TagsText), nameof(VersionsText),
        nameof(ShownVersions), nameof(HasMoreVersions), nameof(OffersVersionActions), nameof(ShowVersionsText),
        nameof(HasPublisher), nameof(HasShortDescription),
        nameof(HasDescription), nameof(HasLicense), nameof(HasHomepageUri), nameof(HasPublisherUri), nameof(HasSupportUri),
        nameof(HasPrivacyUri), nameof(HasLicenseUri), nameof(HasReleaseNotesUri), nameof(HasTags), nameof(HasVersions))]
    public partial PackageDetails? Details { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; private set; }

    /// <summary>The version picked in the list; its action is the one button under it (014 R1.1).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowVersionAction), nameof(CanRunVersion), nameof(VersionActionText), nameof(VersionActionName))]
    [NotifyCanExecuteChangedFor(nameof(RunVersionCommand))]
    public partial VersionItem? SelectedVersion { get; set; }

    /// <summary>Show all versions was pressed for this package (014 R1.3).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShownVersions), nameof(HasMoreVersions))]
    public partial bool ShowingAllVersions { get; private set; }

    /// <summary>The list shows this many until Show all versions (014 R1.3).</summary>
    public const int VersionsShown = 10;

    /// <summary>014 R1.5: actions only where there's a choice, and only for a package WinGet manages.</summary>
    public bool OffersVersionActions => Details is { Versions.Count: > 1 } && Row?.Package.IsManaged == true;

    /// <summary>Today's line of text, where no action is offered (014 R1.5).</summary>
    public bool ShowVersionsText => HasVersions && !OffersVersionActions;

    /// <summary>The newest 10, or all of them (014 R1.3), with the installed one marked.</summary>
    public IReadOnlyList<VersionItem> ShownVersions => Details is { } d
        ? [.. d.Versions.Take(ShowingAllVersions ? d.Versions.Count : VersionsShown)
            .Select(v => new VersionItem(v, VersionLabel(v, Row?.Package.InstalledVersion)))]
        : [];

    public bool HasMoreVersions => OffersVersionActions && !ShowingAllVersions && Details is { Versions.Count: > VersionsShown };

    /// <summary>The action button shows for a selected version that isn't the installed one (014 R1.1).</summary>
    public bool ShowVersionAction => OffersVersionActions && SelectedVersion is { Version.Direction: not VersionDirection.Installed };

    /// <summary>Not while the package is queued or being changed (014 R1.5, 003 R4.2).</summary>
    public bool CanRunVersion => ShowVersionAction && Row?.IsBusy == false;

    public string VersionActionText => SelectedVersion?.Version.Direction switch
    {
        VersionDirection.NotInstalled => "Install",
        VersionDirection.Newer => "Update to this version",
        VersionDirection.Older => "Go back to this version",
        _ => "Install this version",
    };

    /// <summary>The button's accessible name, with the package and the version (014 R1.4).</summary>
    public string VersionActionName => SelectedVersion is not { } item ? "" : (item.Version.Direction, Row?.Name ?? "", item.Version.Version) switch
    {
        (VersionDirection.Newer, var name, var v) => $"Update {name} to {v}",
        (VersionDirection.Older, var name, var v) => $"Go back to {name} {v}",
        (_, var name, var v) => $"Install {name} {v}",
    };

    [RelayCommand]
    private void ShowAllVersions() => ShowingAllVersions = true;

    [RelayCommand(CanExecute = nameof(CanRunVersion))]
    private Task RunVersionAsync() => SelectedVersion is { } item && Row is { } row ? row.InstallVersionAsync(item.Version) : Task.CompletedTask;

    // The button follows the row's busy state while the pane is open (014 R1.5).
    partial void OnRowChanged(PackageRowViewModel? oldValue, PackageRowViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.PropertyChanged -= OnRowPropertyChanged;
        }
        if (newValue is not null)
        {
            newValue.PropertyChanged += OnRowPropertyChanged;
        }
    }

    private void OnRowPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PackageRowViewModel.IsBusy))
        {
            OnPropertyChanged(nameof(CanRunVersion));
            RunVersionCommand.NotifyCanExecuteChanged();
        }
    }


    public bool IsLoading => State == DetailsState.Loading;

    public string? PartialNote => State switch
    {
        DetailsState.Partial when Row?.Package.IsManaged == false =>
            "This app wasn't installed through a WinGet source, so there are no details to show.",
        DetailsState.Partial => "WinGet has no further details for this package.",
        _ => null,
    };

    public Uri? HomepageUri => SafeUri(Details?.HomepageUrl);
    public Uri? PublisherUri => SafeUri(Details?.PublisherUrl);
    public Uri? SupportUri => SafeUri(Details?.SupportUrl);
    public Uri? PrivacyUri => SafeUri(Details?.PrivacyUrl);
    public Uri? LicenseUri => SafeUri(Details?.LicenseUrl);
    public Uri? ReleaseNotesUri => SafeUri(Details?.ReleaseNotesUrl);

    public string? TagsText => Details is { Tags.Count: > 0 } d ? string.Join(" · ", d.Tags) : null;

    // Plain bools for the view: x:Bind doesn't call a function again once its argument is null (011 T8).
    public bool HasError => ErrorMessage is not null;
    public bool HasPartialNote => PartialNote is not null;
    public bool HasPublisher => !string.IsNullOrEmpty(Details?.Publisher);
    public bool HasShortDescription => !string.IsNullOrEmpty(Details?.ShortDescription);
    public bool HasDescription => !string.IsNullOrEmpty(Details?.Description);
    public bool HasLicense => !string.IsNullOrEmpty(Details?.License);
    public bool HasHomepageUri => HomepageUri is not null;
    public bool HasPublisherUri => PublisherUri is not null;
    public bool HasSupportUri => SupportUri is not null;
    public bool HasPrivacyUri => PrivacyUri is not null;
    public bool HasLicenseUri => LicenseUri is not null;
    public bool HasReleaseNotesUri => ReleaseNotesUri is not null;
    public bool HasTags => TagsText is not null;
    public bool HasVersions => VersionsText is not null;

    public bool HasReleaseNotes => ReleaseNotes is { Blocks.Count: > 0 };

    public bool HasGitHubNotesMessage => GitHubNotesMessage is not null;

    /// <summary>
    /// The pane's one status line, a live region: its text changes from loading to the outcome, so a screen reader
    /// hears both (Q5).
    /// </summary>
    public string? GitHubNotesStatus => IsLoadingGitHubNotes ? "Loading notes from GitHub…" : GitHubNotesMessage;

    public bool HasGitHubNotesStatus => GitHubNotesStatus is not null;

    public bool NotesFromGitHub => GitHubNotesHeader is not null;

    /// <summary>The "Show notes from GitHub" button (012 R2.1, R2.2). Retry takes its place when it's offered.</summary>
    public bool CanShowGitHubNotes => GitHubRelease is not null && !IsLoadingGitHubNotes && !GitHubNotesLoaded && !CanRetryGitHubNotes;

    /// <summary>The section also shows for a package that has only a GitHub link.</summary>
    public bool ShowReleaseNotesSection => HasReleaseNotes || CanShowGitHubNotes || IsLoadingGitHubNotes || HasGitHubNotesMessage;



    /// <summary>"1.0 (installed)" for the installed version, else the version (005 R2.1).</summary>
    internal static string VersionLabel(PackageVersion v, string? installed) =>
        v.Direction == VersionDirection.Installed || v.Version == installed ? $"{v.Version} (installed)" : v.Version;

    /// <summary>Newest first, the installed one marked (005 R2.1).</summary>
    public string? VersionsText
    {
        get
        {
            if (Details is not { Versions.Count: > 0 } d)
            {
                return null;
            }
            const int shown = 15;
            var installed = Row?.Package.InstalledVersion;
            var parts = d.Versions.Take(shown).Select(v => VersionLabel(v, installed));
            var text = string.Join(" · ", parts);
            return d.Versions.Count > shown ? $"{text} · and {d.Versions.Count - shown} older" : text;
        }
    }

    /// <summary>Opens the pane for a row; a load still running for another row is cancelled (as 001 R3.2).</summary>
    public async Task LoadAsync(PackageRowViewModel row)
    {
        _currentLoad?.Cancel();
        using var load = new CancellationTokenSource();
        _currentLoad = load;

        Row = row;
        IsOpen = true;
        Details = null;
        ErrorMessage = null;
        SelectedVersion = null;
        ShowingAllVersions = false; // 014 R1.3: per package
        ResetReleaseNotes();
        State = DetailsState.Loading;

        try
        {
            var details = await packages.GetDetailsAsync(row.Package, load.Token);
            if (load.IsCancellationRequested)
            {
                return;
            }
            Details = details;
            ReleaseNotes = WinGetNotesParser.Parse(details?.ReleaseNotes);
            GitHubRelease = GitHubRelease.TryParse(details?.ReleaseNotesUrl);
            State = details is null ? DetailsState.Partial : DetailsState.Loaded;

            // 012 R2.9: notes loaded earlier this session show at once, with no request.
            if (GitHubRelease is { } release && releaseNotes.TryGetCached(release) is { } cached)
            {
                await ShowGitHubNotesAsync(cached, load.Token);
            }
        }
        catch (OperationCanceledException) when (load.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (!load.IsCancellationRequested)
        {
            // Still show what the row knows (R1.5).
            ErrorMessage = ex is WinGetException ? ex.Message : $"Couldn't load details: {ex.Message}";
            State = DetailsState.Error;
        }
        finally
        {
            if (_currentLoad == load)
            {
                _currentLoad = null;
            }
        }
    }

    /// <summary>Closes the pane (005 R4.1). The page returns focus to the row.</summary>
    [RelayCommand]
    public void Close()
    {
        _currentLoad?.Cancel();
        CancelGitHubNotes();
        IsOpen = false;
    }

    /// <summary>"Show notes from GitHub" and Retry (012 R2.2, R2.3). Only ever runs on the user's press.</summary>
    [RelayCommand]
    private async Task LoadGitHubNotesAsync()
    {
        if (GitHubRelease is not { } release)
        {
            return;
        }
        CancelGitHubNotes();
        using var load = new CancellationTokenSource();
        _notesLoad = load;
        GitHubNotesMessage = null;
        CanRetryGitHubNotes = false;
        IsLoadingGitHubNotes = true;
        try
        {
            var result = await releaseNotes.GetGitHubNotesAsync(release, load.Token);
            if (load.IsCancellationRequested)
            {
                return; // R2.10: a late answer is dropped
            }
            if (result.Notes is { } notes)
            {
                await ShowGitHubNotesAsync(notes, load.Token);
            }
            else
            {
                ShowGitHubFailure(result);
            }
        }
        catch (OperationCanceledException) when (load.IsCancellationRequested)
        {
        }
        catch (Exception) when (!load.IsCancellationRequested)
        {
            ShowGitHubFailure(new GitHubNotesResult(GitHubNotesOutcome.Failed));
        }
        finally
        {
            if (_notesLoad == load)
            {
                _notesLoad = null;
                IsLoadingGitHubNotes = false;
            }
        }
    }

    private async Task ShowGitHubNotesAsync(GitHubNotes notes, CancellationToken cancellationToken)
    {
        // Bodies can be long (up to 125,000 characters): parse off the UI thread.
        var doc = await Task.Run(() => GitHubMarkdown.Parse(notes.Body, notes.Page, notes.Title), cancellationToken);
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        GitHubNotesLoaded = true;
        if (doc.Blocks.Count == 0)
        {
            // WinGet's text, if any, stays.
            GitHubNotesMessage = "This GitHub release has no notes.";
            return;
        }
        ReleaseNotes = doc;
        GitHubNotesHeader = notes.PublishedAt is { } published
            ? $"{notes.Title} · {published.ToLocalTime().ToString("d MMMM yyyy", CultureInfo.CurrentCulture)} · from GitHub"
            : $"{notes.Title} · from GitHub";
    }

    /// <summary>R2.11: WinGet's notes stay; a short line says what happened.</summary>
    private void ShowGitHubFailure(GitHubNotesResult result)
    {
        (GitHubNotesMessage, CanRetryGitHubNotes) = result.Outcome switch
        {
            GitHubNotesOutcome.Unreachable => ("Couldn't reach GitHub.", true),
            GitHubNotesOutcome.RateLimited when result.RetryAt is { } at =>
                ($"GitHub limits requests without sign-in. Try again after {at.ToLocalTime().ToString("t", CultureInfo.CurrentCulture)}.", false),
            GitHubNotesOutcome.NotFound => ("GitHub has no release with this tag.", false),
            _ => ("Couldn't load the notes from GitHub.", true),
        };
    }

    private void CancelGitHubNotes()
    {
        _notesLoad?.Cancel();
        _notesLoad = null;
        IsLoadingGitHubNotes = false;
    }

    private void ResetReleaseNotes()
    {
        CancelGitHubNotes();
        ReleaseNotes = null;
        GitHubRelease = null;
        GitHubNotesLoaded = false;
        GitHubNotesMessage = null;
        CanRetryGitHubNotes = false;
        GitHubNotesHeader = null;
    }


    /// <summary>Only web links are opened; anything else (file:, ms-settings:…) stays plain text (005 security).</summary>
    public static Uri? SafeUri(string? url) => WebLinks.SafeUri(url);

    /// <summary>Icons load with no user action, so only https (005 R6.1). Links stay on <see cref="SafeUri"/>: the user clicks those.</summary>
    public static Uri? SafeIconUri(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps ? uri : null;
}
