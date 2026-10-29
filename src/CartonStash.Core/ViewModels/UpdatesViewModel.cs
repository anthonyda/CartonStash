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
    private IReadOnlyList<PackageRowViewModel> _selected = [];

    public UpdatesViewModel(InstalledPackagesStore store, PackageActions actions, IPackageService packages)
    {
        _store = store;
        _actions = actions;
        Details = new PackageDetailsViewModel(packages);
        _store.PropertyChanged += OnStoreChanged;
        RowOperationTracker.Track(actions, () => Updates);
        Rebuild();
    }

    public ObservableCollection<PackageRowViewModel> Updates { get; } = [];

    public PackageDetailsViewModel Details { get; }

    public bool IsLoading => _store.IsLoading;

    public string? ErrorMessage => _store.ErrorMessage;

    /// <summary>For the navigation badge (R4.1); null until the installed data is loaded.</summary>
    public int? Count => _store.IsLoaded && _store.ErrorMessage is null ? Updates.Count : null;

    /// <summary>"Everything is up to date" once loaded with nothing to update (R5.1).</summary>
    public bool IsUpToDate => Count == 0;

    public string Title => Count is > 0 and var n ? $"Updates ({n})" : "Updates";

    public bool HasSelection => _selected.Count > 0;

    public Task LoadAsync() => _store.EnsureLoadedAsync();

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
        await _actions.UpdateManyAsync(_selected.Where(r => !r.IsBusy).Select(r => r.Package).ToList(), confirm: false);

    /// <summary>Confirms with the package count, then queues them all (R2.3, constitution P2).</summary>
    [RelayCommand]
    private async Task UpdateAllAsync() =>
        await _actions.UpdateManyAsync(Updates.Where(r => !r.IsBusy && r.ShowUpdate).Select(r => r.Package).ToList(), confirm: true);

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
                NotifyCount();
                break;
        }
    }

    // Updated packages drop out here after the store reloads (R2.4).
    private void Rebuild()
    {
        Updates.Clear();
        foreach (var package in _store.Packages
                     .Where(p => p.HasUpdate && p.IsManaged)
                     .OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            Updates.Add(new PackageRowViewModel(package, _actions, RowActions.Update));
        }
        SetSelection([]);
        NotifyCount();
    }

    private void NotifyCount()
    {
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(IsUpToDate));
        OnPropertyChanged(nameof(Title));
    }
}
