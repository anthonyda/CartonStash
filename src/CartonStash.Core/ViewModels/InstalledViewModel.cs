using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CartonStash.Core.Services;

namespace CartonStash.Core.ViewModels;

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

    /// <summary>The selected status chip (R8.2); null = All.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAllSelected), nameof(IsSameSelected), nameof(IsUpdateSelected), nameof(IsNewerSelected), nameof(IsOtherSelected))]
    public partial VersionBadge? StatusFilter { get; set; }

    // Two-way targets for the chip ToggleButtons: checking one selects it, unchecking the selected one clears the filter.
    public bool IsAllSelected
    {
        get => StatusFilter is null;
        set
        {
            if (value)
            {
                StatusFilter = null;
            }
            else
            {
                OnPropertyChanged(); // "All" stays checked until another chip is chosen
            }
        }
    }

    public bool IsSameSelected { get => StatusFilter == VersionBadge.Same; set => Select(VersionBadge.Same, value); }

    public bool IsUpdateSelected { get => StatusFilter == VersionBadge.UpdateOffered; set => Select(VersionBadge.UpdateOffered, value); }

    public bool IsNewerSelected { get => StatusFilter == VersionBadge.Newer; set => Select(VersionBadge.Newer, value); }

    public bool IsOtherSelected { get => StatusFilter == VersionBadge.Neutral; set => Select(VersionBadge.Neutral, value); }

    /// <summary>Rows per state after the text and unmanaged filters (R8.3).</summary>
    [ObservableProperty]
    public partial int SameCount { get; private set; }

    [ObservableProperty]
    public partial int UpdateCount { get; private set; }

    [ObservableProperty]
    public partial int NewerCount { get; private set; }

    [ObservableProperty]
    public partial int OtherCount { get; private set; }

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

    partial void OnStatusFilterChanged(VersionBadge? value) => ApplyFilter();

    private void Select(VersionBadge state, bool selected)
    {
        if (selected)
        {
            StatusFilter = state;
        }
        else if (StatusFilter == state)
        {
            StatusFilter = null;
        }
    }

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

    // In memory only: never queries WinGet again (R2.1, R8.2).
    private void ApplyFilter()
    {
        var text = Filter.Trim();
        int same = 0, update = 0, newer = 0, other = 0;
        Visible.Clear();
        foreach (var row in _all)
        {
            if (!ShowUnmanaged && !row.Package.IsManaged)
            {
                continue;
            }
            if (text.Length > 0
                && !row.Name.Contains(text, StringComparison.CurrentCultureIgnoreCase)
                && !row.Id.Contains(text, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Counts describe what the text and unmanaged filters leave (R8.3).
            var state = row.VersionBadge;
            switch (state)
            {
                case VersionBadge.Same: same++; break;
                case VersionBadge.UpdateOffered: update++; break;
                case VersionBadge.Newer: newer++; break;
                default: other++; break;
            }

            if (StatusFilter is null || StatusFilter == state)
            {
                Visible.Add(row);
            }
        }
        (SameCount, UpdateCount, NewerCount, OtherCount) = (same, update, newer, other);
        OnPropertyChanged(nameof(CountText));
    }
}
