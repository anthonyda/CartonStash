using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CartonStash.Core.Models;
using CartonStash.Core.Services;

namespace CartonStash.Core.ViewModels;

/// <summary>Search screen. Behaviour is defined in specs/001-package-search; rows offer Install (003 R1.1).</summary>
public sealed partial class SearchViewModel : ObservableObject
{
    private readonly IPackageService _packages;
    private readonly PackageActions _actions;
    private readonly SourceAgreementCheck _agreements;
    private CancellationTokenSource? _currentSearch;

    public SearchViewModel(IPackageService packages, PackageActions actions, SourceAgreementCheck agreements, IReleaseNotesService releaseNotes)
    {
        _packages = packages;
        _actions = actions;
        _agreements = agreements;
        agreements.Changed += (_, _) => NotInUseNotice = DescribeNotInUse(agreements.NotAccepted);
        NotInUseNotice = DescribeNotInUse(agreements.NotAccepted);
        Details = new PackageDetailsViewModel(packages, releaseNotes);
        RowOperationTracker.Track(actions, () => Results);
    }

    public ObservableCollection<PackageRowViewModel> Results { get; } = [];

    public PackageDetailsViewModel Details { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchCommand))]
    public partial string Query { get; set; } = "";

    [ObservableProperty]
    public partial bool IsSearching { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

    /// <summary>Set when a finished search found nothing (R3.3); null otherwise.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEmptyMessage))]
    public partial string? EmptyMessage { get; set; }

    /// <summary>Names the sources missing from a finished search, and why (011 R2.1); null when none are.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSourceNotice))]
    public partial string? SourceNotice { get; set; }

    /// <summary>Names the sources not accepted, shown with Read and accept (006 R3.10); null when there are none.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNotInUseNotice))]
    public partial string? NotInUseNotice { get; private set; }

    // Plain bools for the view: x:Bind doesn't call a function again once its argument is null (011 T8).
    public bool HasError => ErrorMessage is not null;

    public bool HasEmptyMessage => EmptyMessage is not null;

    public bool HasSourceNotice => SourceNotice is not null;

    public bool HasNotInUseNotice => NotInUseNotice is not null;

    private bool CanSearch() => !string.IsNullOrWhiteSpace(Query);

    internal static string? DescribeNotInUse(IReadOnlyList<SourceInfo> sources) =>
        sources.Count == 0 ? null
        : sources.Count == 1 ? $"'{sources[0].Name}' isn't in use, so results leave it out."
        : $"{string.Join(", ", sources.Select(s => $"'{s.Name}'"))} aren't in use, so results leave them out.";

    /// <summary>Asks about each source not in use, then repeats the search with what was accepted (R3.10).</summary>
    [RelayCommand]
    private async Task ReadAndAcceptAsync()
    {
        if (await _agreements.ReadAndAcceptAsync() && CanSearch())
        {
            await SearchCommand.ExecuteAsync(null);
        }
    }

    // Concurrent executions are allowed so a new search can supersede a running one (R3.2).
    [RelayCommand(CanExecute = nameof(CanSearch), AllowConcurrentExecutions = true)]
    private async Task SearchAsync()
    {
        _currentSearch?.Cancel();
        using var search = new CancellationTokenSource();
        _currentSearch = search;

        var query = Query.Trim();
        ErrorMessage = null;
        EmptyMessage = null;
        SourceNotice = null;
        Results.Clear();
        IsSearching = true;

        try
        {
            var found = await _packages.SearchAsync(query, cancellationToken: search.Token);
            if (search.IsCancellationRequested)
            {
                return;
            }

            SourceNotice = SourceFailureMessages.Notice(_packages.SourceFailures);
            foreach (var package in found)
            {
                Results.Add(new PackageRowViewModel(package, _actions, RowActions.Install));
            }
            if (Results.Count == 0)
            {
                EmptyMessage = $"No packages found for '{query}'";
            }
        }
        catch (OperationCanceledException) when (search.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (!search.IsCancellationRequested)
        {
            // Query is left untouched so the user can retry (R3.4).
            ErrorMessage = ex is WinGetException ? ex.Message : $"The search failed unexpectedly: {ex.Message}";
        }
        finally
        {
            if (_currentSearch == search)
            {
                _currentSearch = null;
                IsSearching = false;
            }
        }
    }
}
