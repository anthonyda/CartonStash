using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinGetFrontend.Core.Models;
using WinGetFrontend.Core.Services;

namespace WinGetFrontend.Core.ViewModels;

/// <summary>Search screen. Behaviour is defined in specs/001-package-search.</summary>
public sealed partial class SearchViewModel(IPackageService packages) : ObservableObject
{
    private CancellationTokenSource? _currentSearch;

    public ObservableCollection<PackageInfo> Results { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchCommand))]
    public partial string Query { get; set; } = "";

    [ObservableProperty]
    public partial bool IsSearching { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    /// <summary>Set when a finished search found nothing (R3.3); null otherwise.</summary>
    [ObservableProperty]
    public partial string? EmptyMessage { get; set; }

    private bool CanSearch() => !string.IsNullOrWhiteSpace(Query);

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
        Results.Clear();
        IsSearching = true;

        try
        {
            var found = await packages.SearchAsync(query, cancellationToken: search.Token);
            if (search.IsCancellationRequested)
            {
                return;
            }

            foreach (var package in found)
            {
                Results.Add(package);
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
