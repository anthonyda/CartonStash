using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CartonStash.Core.Services;

namespace CartonStash.Core.ViewModels;

/// <summary>
/// Updates page (spec 004): a view over the installed list where WinGet reports an update (R1.1, R1.2).
/// R3 (explicit targeting / pins) is deferred: every listed package is included in Update all.
/// </summary>
public sealed partial class UpdatesViewModel : ObservableObject
{
    private readonly InstalledPackagesStore _store;
    private readonly PackageActions _actions;
    private readonly ISettings _settings;
    private IReadOnlyList<string> _excludedIds = [];
    private IReadOnlyList<PackageRowViewModel> _selected = [];

    public UpdatesViewModel(InstalledPackagesStore store, PackageActions actions, IPackageService packages, ISettings settings, IReleaseNotesService releaseNotes)
    {
        _store = store;
        _actions = actions;
        _settings = settings;
        Details = new PackageDetailsViewModel(packages, releaseNotes);
        _store.PropertyChanged += OnStoreChanged;
        _settings.Changed += OnSettingsChanged;
        RowOperationTracker.Track(actions, () => Updates.Concat(Excluded));
        actions.Host.Changed += (_, _) => Rebuild(); // 004 R6.2, R6.4
        Rebuild();
    }

    public ObservableCollection<PackageRowViewModel> Updates { get; } = [];

    /// <summary>Updates the user left out of checks: still listed, and still updatable one at a time (010 R5.5).</summary>
    public ObservableCollection<PackageRowViewModel> Excluded { get; } = [];

    public bool HasExcluded => Excluded.Count > 0;

    public PackageDetailsViewModel Details { get; }

    public bool IsLoading => _store.IsLoading;

    public string? ErrorMessage => _store.ErrorMessage;

    /// <summary>A plain bool for the InfoBar: x:Bind doesn't call a function again once its argument is null (011 T8).</summary>
    public bool HasError => ErrorMessage is not null;

    /// <summary>For the navigation badge (R4.1); null until the installed data is loaded.</summary>
    public int? Count => _store.IsLoaded && _store.ErrorMessage is null ? Updates.Count : null;

    /// <summary>"Everything is up to date" once loaded with nothing to update (R5.1).</summary>
    public bool IsUpToDate => Count == 0;

    public string Title => Count is > 0 and var n ? $"Updates ({n})" : "Updates";

    public bool HasSelection => _selected.Count > 0;

    public Task LoadAsync() => _store.EnsureLoadedAsync();

    /// <summary>004 R6.4: after a restart for App Installer's update, true if the old version is still the one in use.</summary>
    public async Task<bool> CheckAfterRestartAsync(string pendingVersion)
    {
        await _store.EnsureLoadedAsync();
        _actions.Host.CheckAfterRestart(pendingVersion, _store.Packages);
        return _actions.Host.StillPendingAfterRestart;
    }

    /// <summary>The page reports the checked rows (R2.2).</summary>
    public void SetSelection(IReadOnlyList<PackageRowViewModel> rows)
    {
        _selected = rows;
        OnPropertyChanged(nameof(HasSelection));
        UpdateSelectedCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task RefreshAsync() => _store.ReloadAsync();

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task UpdateSelectedAsync() =>
        await _actions.UpdateManyAsync(_selected.Where(r => r.ShowUpdate).Select(r => r.Package).ToList()); // BR-21: the row's own rule

    /// <summary>Queues every row that offers Update, with no confirmation (R9.1, constitution P2 as amended 2026-11-12).</summary>
    [RelayCommand]
    private async Task UpdateAllAsync() =>
        await _actions.UpdateManyAsync(Updates.Where(r => r.ShowUpdate).Select(r => r.Package).ToList());

    private void OnStoreChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(InstalledPackagesStore.Packages):
                Rebuild();
                break;
            case nameof(InstalledPackagesStore.IsLoading):
                OnPropertyChanged(nameof(IsLoading));
                break;
            case nameof(InstalledPackagesStore.ErrorMessage):
            case nameof(InstalledPackagesStore.IsLoaded):
                OnPropertyChanged(nameof(ErrorMessage));
                OnPropertyChanged(nameof(HasError));
                NotifyCount();
                break;
        }
    }

    // Updated packages drop out here after the store reloads (R2.4).
    // R8.1: rows still listed stay the same objects in place; only rows that left are removed and new ones inserted.
    // Never Clear (see RowListSync).
    private void Rebuild()
    {
        _excludedIds = _settings.Current.ExcludedPackageIds;
        var existing = Updates.Concat(Excluded)
            .GroupBy(r => r.Package.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => new Queue<PackageRowViewModel>(g), StringComparer.OrdinalIgnoreCase);
        List<PackageRowViewModel> updates = [], excluded = [];
        foreach (var package in _store.Packages
                     .Where(p => p.HasUpdate && p.IsManaged)
                     .OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            PackageRowViewModel row;
            if (existing.TryGetValue(package.Key, out var same) && same.Count > 0)
            {
                row = same.Dequeue();
                row.Refresh(package);
                row.NotifyStateChanged(); // App Installer's restart state and the exclusion aren't part of the package (R6.2)
            }
            else
            {
                row = new PackageRowViewModel(package, _actions, RowActions.Update, _settings);
            }
            (row.IsExcluded ? excluded : updates).Add(row);
        }
        RowListSync.Sync(Updates, updates);
        RowListSync.Sync(Excluded, excluded);
        OnPropertyChanged(nameof(HasExcluded));
        SetSelection(_selected.Where(Updates.Contains).ToList()); // R8.2: checked rows that stay keep their check
        NotifyCount();
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        if (!_excludedIds.SequenceEqual(_settings.Current.ExcludedPackageIds))
        {
            Rebuild();
        }
    }

    private void NotifyCount()
    {
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(IsUpToDate));
        OnPropertyChanged(nameof(Title));
    }
}
