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

/// <summary>The details pane (spec 005). Each list page owns one.</summary>
public sealed partial class PackageDetailsViewModel(IPackageService packages) : ObservableObject
{
    /// <summary>Release notes longer than this are collapsed behind "Show more" (005 R2.2).</summary>
    public const int CollapsedReleaseNoteLines = 10;

    private CancellationTokenSource? _currentLoad;

    [ObservableProperty]
    public partial bool IsOpen { get; private set; }

    /// <summary>The row the pane shows; its actions and progress are the row's (005 R3.1).</summary>
    [ObservableProperty]
    public partial PackageRowViewModel? Row { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoading), nameof(PartialNote))]
    public partial DetailsState State { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HomepageUri), nameof(PublisherUri), nameof(SupportUri), nameof(PrivacyUri),
        nameof(LicenseUri), nameof(ReleaseNotesUri), nameof(TagsText), nameof(VersionsText),
        nameof(ReleaseNotesIsLong), nameof(HasReleaseNotes))]
    public partial PackageDetails? Details { get; private set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReleaseNotesMaxLines))]
    public partial bool ShowAllReleaseNotes { get; set; }

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

    public bool HasReleaseNotes => Details?.ReleaseNotes is not null;

    public bool ReleaseNotesIsLong => Details?.ReleaseNotes is { } notes && notes.Split('\n').Length > CollapsedReleaseNoteLines;

    /// <summary>0 means unlimited (TextBlock.MaxLines).</summary>
    public int ReleaseNotesMaxLines => ShowAllReleaseNotes ? 0 : CollapsedReleaseNoteLines;

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
            var parts = d.Versions.Take(shown).Select(v => v == installed ? $"{v} (installed)" : v);
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
        ShowAllReleaseNotes = false;
        State = DetailsState.Loading;

        try
        {
            var details = await packages.GetDetailsAsync(row.Package, load.Token);
            if (load.IsCancellationRequested)
            {
                return;
            }
            Details = details;
            State = details is null ? DetailsState.Partial : DetailsState.Loaded;
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
        IsOpen = false;
    }

    [RelayCommand]
    private void ToggleReleaseNotes() => ShowAllReleaseNotes = !ShowAllReleaseNotes;

    /// <summary>Only web links are opened; anything else (file:, ms-settings:…) stays plain text (005 security).</summary>
    public static Uri? SafeUri(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? uri
            : null;

    /// <summary>Icons load with no user action, so only https (005 R6.1). Links stay on <see cref="SafeUri"/>: the user clicks those.</summary>
    public static Uri? SafeIconUri(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps ? uri : null;
}
