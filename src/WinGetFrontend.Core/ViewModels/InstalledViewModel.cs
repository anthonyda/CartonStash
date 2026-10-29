using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinGetFrontend.Core.Services;

namespace WinGetFrontend.Core.ViewModels;

/// <summary>Installed page (spec 002). Rows offer Uninstall (003 R2.1).</summary>
public sealed partial class InstalledViewModel : ObservableObject
{
    private readonly InstalledPackagesStore _store;
    private readonly PackageActions _actions;
    private List<PackageRowViewModel> _all = [];

    public InstalledViewModel(InstalledPackagesStore store, PackageActions actions, IPackageService packages)
    {
        _store = store;
        _actions = actions;
        Details = new PackageDetailsViewModel(packages);
        _store.PropertyChanged += OnStoreChanged;
        RowOperationTracker.Track(actions, () => _all);
        Rebuild();
    }

    /// <summary>The filtered, name-sorted rows (R2.1, R2.3).</summary>
    public ObservableCollection<PackageRowViewModel> Visible { get; } = [];

    public PackageDetailsViewModel Details { get; }

    [ObservableProperty]
    public partial string Filter { get; set; } = "";

    /// <summary>Show apps WinGet doesn't manage; off by default (R2.4).</summary>
    [ObservableProperty]
    public partial bool ShowUnmanaged { get; set; }

    public bool IsLoading => _store.IsLoading;

    public string? ErrorMessage => _store.ErrorMessage;

    public int TotalCount => _all.Count;

    /// <summary>"42 of 318 packages" (R2.2).</summary>
    public string CountText => _store.IsLoaded || _all.Count > 0 ? $"{Visible.Count} of {_all.Count} packages" : "";

    /// <summary>First visit loads; later visits reuse the session's list (R1.3).</summary>
    public Task LoadAsync() => _store.EnsureLoadedAsync();

    /// <summary>Refresh and Retry (R1.3, R5.1).</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task RefreshAsync() => _store.ReloadAsync();

    partial void OnFilterChanged(string value) => ApplyFilter();

    partial void OnShowUnmanagedChanged(bool value) => ApplyFilter();

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
                OnPropertyChanged(nameof(ErrorMessage));
                break;
            case nameof(InstalledPackagesStore.IsLoaded):
                OnPropertyChanged(nameof(CountText));
                break;
        }
    }

    private void Rebuild()
    {
        _all = _store.Packages
            .OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
            .Select(p => new PackageRowViewModel(p, _actions, RowActions.Uninstall))
            .ToList();
        OnPropertyChanged(nameof(TotalCount));
        ApplyFilter();
    }

    // In memory only: never queries WinGet again (R2.1).
    private void ApplyFilter()
    {
        var text = Filter.Trim();
        Visible.Clear();
        foreach (var row in _all)
        {
            if (!ShowUnmanaged && !row.Package.IsManaged)
            {
                continue;
            }
            if (text.Length == 0
                || row.Name.Contains(text, StringComparison.CurrentCultureIgnoreCase)
                || row.Id.Contains(text, StringComparison.OrdinalIgnoreCase))
            {
                Visible.Add(row);
            }
        }
        OnPropertyChanged(nameof(CountText));
    }
}
