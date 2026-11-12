using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CartonStash.Core.Models;
using CartonStash.Core.Services;

namespace CartonStash.Core.ViewModels;

public enum NotificationSeverity
{
    Informational,
    Success,
    Warning,
    Error,
}

/// <summary>
/// The window around the pages (002 R4.1): navigation badges, the latest operation result (003 R5.x),
/// and the close guard (003 R6.2).
/// </summary>
public sealed partial class ShellViewModel : ObservableObject
{
    private readonly PackageOperationQueue _queue;
    private readonly UpdatesViewModel _updates;
    private readonly IDialogService _dialogs;
    private readonly WinGetHost? _host;
    private readonly IAppRestarter? _restarter;
    private readonly ITaskbarBadge? _badge;
    private int? _badgeShown;

    public ShellViewModel(PackageOperationQueue queue, UpdatesViewModel updates, IDialogService dialogs, ISettings settings,
        WinGetHost? host = null, IAppRestarter? restarter = null, ITaskbarBadge? badge = null)
    {
        _queue = queue;
        _updates = updates;
        _dialogs = dialogs;
        _host = host;
        _restarter = restarter;
        _badge = badge;

        queue.Completed += (_, operation) => ShowOutcome(operation);
        queue.RestartRequested += async (_, _) => await RestartAsync();
        queue.Enqueued += (_, _) => OnPropertyChanged(nameof(ActivityText));
        queue.Operations.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ActivityText));
        updates.PropertyChanged += OnUpdatesChanged;

        if (settings.WasReset)
        {
            Notify("Settings were reset", "Your settings file couldn't be read, so the defaults are used.", NotificationSeverity.Warning);
        }
    }

    /// <summary>Updates badge value (004 R4.1). 0 hides the badge.</summary>
    public int UpdatesCount => _updates.Count ?? 0;

    public bool HasUpdates => UpdatesCount > 0;

    public bool HasPendingWork => _queue.HasPendingWork;

    /// <summary>"Activity (2)" while operations are queued or running.</summary>
    public string ActivityText => _queue.ActiveCount > 0 ? $"Activity ({_queue.ActiveCount})" : "Activity";

    [ObservableProperty]
    public partial bool IsNotificationOpen { get; set; }

    [ObservableProperty]
    public partial string NotificationTitle { get; private set; } = "";

    [ObservableProperty]
    public partial string NotificationMessage { get; private set; } = "";

    [ObservableProperty]
    public partial NotificationSeverity NotificationSeverity { get; private set; }

    /// <summary>The notification's button, when it has one: Restart CartonStash (004 R6.3).</summary>
    [ObservableProperty]
    public partial bool HasNotificationRestart { get; private set; }

    public void Notify(string title, string message, NotificationSeverity severity)
    {
        HasNotificationRestart = false;
        NotificationTitle = title;
        NotificationMessage = message;
        NotificationSeverity = severity;
        IsNotificationOpen = true;
    }

    /// <summary>
    /// Asked when the window is closing. True means close now; false means keep the app open (003 R6.2).
    /// </summary>
    public async Task<bool> ConfirmCloseAsync()
    {
        if (!_queue.HasPendingWork)
        {
            return true;
        }

        var n = _queue.ActiveCount;
        return await _dialogs.ConfirmAsync(
            "Operations are still running",
            $"{n} {(n == 1 ? "operation is" : "operations are")} still running or queued. If you quit now, queued ones won't run, "
                + "and an installer that already started keeps running in the background.",
            "Quit anyway",
            "Wait");
    }

    [RelayCommand]
    private void CloseNotification() => IsNotificationOpen = false;

    /// <summary>
    /// 004 R6.4: the new copy after a restart. If App Installer still shows the old version, something else kept WinGet
    /// running: the row keeps offering the restart, and this says what finishes it.
    /// </summary>
    public async Task FinishRestartAsync(string? pendingVersion)
    {
        if (pendingVersion is not null && await _updates.CheckAfterRestartAsync(pendingVersion))
        {
            Notify("App Installer isn't updated yet",
                "It finishes updating once every program using WinGet is closed, such as the winget command or another app.",
                NotificationSeverity.Warning);
        }
    }

    /// <summary>
    /// 004 R6.3: restart to finish App Installer's update. With operations running or queued, asks first, as closing does
    /// (003 R6.2): the restart skips the window's own close guard.
    /// </summary>
    [RelayCommand]
    private async Task RestartAsync()
    {
        if (!await ConfirmCloseAsync())
        {
            return;
        }
        // Ending in the middle of a WinGet call leaves WinGet's server waiting minutes for this copy, so the new one would
        // get the old App Installer (T15). The reload after an update is the call most likely to be running.
        for (var i = 0; i < 100 && _updates.IsLoading; i++)
        {
            await Task.Delay(100);
        }
        if (_restarter?.Restart(_host?.PendingVersion) != true)
        {
            Notify("Couldn't restart CartonStash", "Close CartonStash and open it again to finish updating App Installer.", NotificationSeverity.Error);
        }
    }

    private void ShowOutcome(PackageOperation operation)
    {
        OnPropertyChanged(nameof(ActivityText));
        var severity = operation.Result switch
        {
            OperationResult.Succeeded => NotificationSeverity.Success,
            OperationResult.SucceededRestartRequired or OperationResult.SucceededAppRestartRequired
                or OperationResult.SucceededUnconfirmed => NotificationSeverity.Warning, // R5.3, 004 R6.1, 013 R2.2: say it clearly
            OperationResult.Cancelled => NotificationSeverity.Informational,
            _ => NotificationSeverity.Error,
        };
        Notify($"{operation.Title}: {operation.StatusText}", operation.Message ?? "", severity);
        HasNotificationRestart = operation.CanRestartApp;
    }

    private void OnUpdatesChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(UpdatesViewModel.Count))
        {
            OnPropertyChanged(nameof(UpdatesCount));
            OnPropertyChanged(nameof(HasUpdates));
            ShowOnTaskbar(_updates.Count);
        }
    }

    // 016 R1.1-R1.6: only a count read from WinGet this session; while loading or after an error the badge keeps its value.
    private void ShowOnTaskbar(int? count)
    {
        if (_badge is null || count is not { } n || n == _badgeShown)
        {
            return;
        }
        _badgeShown = n;
        _badge.Show(n);
    }
}

/// <summary>
/// The Activity pane (003, 009): every operation this session with progress, Cancel, Retry and Log,
/// and whether the pane is pinned beside the page.
/// </summary>
public sealed partial class ActivityViewModel : ObservableObject
{
    private readonly PackageOperationQueue _queue;
    private readonly IOperationLogStore _logs;
    private readonly IDialogService _dialogs;
    private readonly ISettings _settings;

    public ActivityViewModel(PackageOperationQueue queue, IOperationLogStore logs, IDialogService dialogs, ISettings settings)
    {
        _queue = queue;
        _logs = logs;
        _dialogs = dialogs;
        _settings = settings;
        ((INotifyCollectionChanged)queue.Operations).CollectionChanged += (_, _) =>
        {
            Watch();
            OnPropertyChanged(nameof(IsEmpty));
            Rearrange();
        };
        queue.LogRequested += async (_, operation) => await ShowLogAsync(operation);
        Watch();
        Rearrange();
    }

    private readonly HashSet<PackageOperation> _watched = [];

    /// <summary>What the list shows: an <see cref="ActivityHeader"/> or a <see cref="PackageOperation"/> (009 R4). Never replaced (R4.6).</summary>
    public System.Collections.ObjectModel.ObservableCollection<object> Entries { get; } = [];

    /// <summary>R4.1, for the session only (R4.8).</summary>
    [ObservableProperty]
    public partial string FilterText { get; set; } = "";

    partial void OnFilterTextChanged(string value) => Rearrange();

    /// <summary>R4.2, R4.3, for the session only (R4.8).</summary>
    [ObservableProperty]
    public partial ActivityTab Tab { get; set; }

    partial void OnTabChanged(ActivityTab value) => Rearrange();

    /// <summary>R4.5, saved in Settings (R4.8).</summary>
    public ActivitySort Sort
    {
        get => _settings.Current.ActivitySort;
        set
        {
            if (value != Sort)
            {
                _settings.Update(s => s with { ActivitySort = value });
                OnPropertyChanged();
                OnPropertyChanged(nameof(SortText));
                Rearrange();
            }
        }
    }

    /// <summary>The Sort button's tooltip and accessible name (R4.5).</summary>
    public string SortText => $"Sort: {SortLabel(Sort)}";

    public static string SortLabel(ActivitySort sort) => sort switch
    {
        ActivitySort.Newest => "Newest first",
        ActivitySort.Oldest => "Oldest first",
        ActivitySort.Name => "Name A to Z",
        _ => "Default",
    };

    /// <summary>Tab labels with their counts, "Active (6)" (R4.2).</summary>
    [ObservableProperty]
    public partial string AllTabText { get; private set; } = "All";

    [ObservableProperty]
    public partial string ActiveTabText { get; private set; } = "Active";

    [ObservableProperty]
    public partial string DoneTabText { get; private set; } = "Done";

    [ObservableProperty]
    public partial string FailedTabText { get; private set; } = "Failed";

    /// <summary>R4.7: there are operations, but none matches the filter and tab.</summary>
    [ObservableProperty]
    public partial bool HasNoMatch { get; private set; }

    /// <summary>R4.7: empties the filter and selects All.</summary>
    [RelayCommand]
    private void ClearFilter()
    {
        FilterText = "";
        Tab = ActivityTab.All;
    }

    // Re-arrange when an operation changes category or time, not on progress ticks (R4.6).
    private void Watch()
    {
        foreach (var gone in _watched.Where(o => !_queue.Operations.Contains(o)).ToList())
        {
            gone.PropertyChanged -= Operation_PropertyChanged;
            _watched.Remove(gone);
        }
        for (var i = 0; i < _queue.Operations.Count; i++)
        {
            if (_watched.Add(_queue.Operations[i]))
            {
                _queue.Operations[i].PropertyChanged += Operation_PropertyChanged;
            }
        }
    }

    private void Operation_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PackageOperation.State) or nameof(PackageOperation.FinishedAt) or nameof(PackageOperation.StartedAt))
        {
            Rearrange();
        }
    }

    private void Rearrange()
    {
        var arrangement = ActivityArranger.Arrange(_queue.Operations, FilterText, Tab, Sort);
        SyncTo(Entries, arrangement.Entries);
        AllTabText = TabText("All", arrangement.All);
        ActiveTabText = TabText("Active", arrangement.Active);
        DoneTabText = TabText("Done", arrangement.Done);
        FailedTabText = TabText("Failed", arrangement.Failed);
        HasNoMatch = _queue.Operations.Count > 0 && Entries.Count == 0;
    }

    private static string TabText(string name, int count) => count > 0 ? $"{name} ({count})" : name;

    /// <summary>
    /// Brings <paramref name="target"/> to <paramref name="wanted"/> with removes and inserts, never a reset, so cards that
    /// keep their place keep their containers (R4.6).
    /// </summary>
    internal static void SyncTo(System.Collections.ObjectModel.ObservableCollection<object> target, IReadOnlyList<object> wanted)
    {
        var keep = new HashSet<object>(wanted);
        for (var i = target.Count - 1; i >= 0; i--)
        {
            if (!keep.Contains(target[i]))
            {
                target.RemoveAt(i);
            }
        }
        for (var i = 0; i < wanted.Count; i++)
        {
            if (i < target.Count && Equals(target[i], wanted[i]))
            {
                continue;
            }
            var from = -1;
            for (var j = i + 1; j < target.Count; j++)
            {
                if (Equals(target[j], wanted[i]))
                {
                    from = j;
                    break;
                }
            }
            // No Move: WinUI's ListView drew moved items out of order and crashed when the sort changed (VM run, 2026-11-12).
            if (from >= 0)
            {
                target.RemoveAt(from);
            }
            target.Insert(i, wanted[i]);
        }
    }

    public System.Collections.ObjectModel.ObservableCollection<PackageOperation> Operations => _queue.Operations;

    public bool IsEmpty => _queue.Operations.Count == 0;

    /// <summary>Saved at once and restored at start (R3.4).</summary>
    public bool IsPinned
    {
        get => _settings.Current.ActivityPinned;
        set
        {
            if (value != IsPinned)
            {
                _settings.Update(s => s with { ActivityPinned = value });
                OnPropertyChanged();
            }
        }
    }

    [RelayCommand]
    private void ClearFinished() => _queue.ClearFinished();

    /// <summary>The Log button of a finished operation (R2.2).</summary>
    public async Task ShowLogAsync(PackageOperation operation) =>
        await _dialogs.ShowLogAsync(operation.Title, await Task.Run(() => _logs.Read(operation)), _logs.Folder); // BR-20: off the UI thread
}
