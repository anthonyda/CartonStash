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

    public ShellViewModel(PackageOperationQueue queue, UpdatesViewModel updates, IDialogService dialogs, ISettings settings,
        WinGetHost? host = null, IAppRestarter? restarter = null)
    {
        _queue = queue;
        _updates = updates;
        _dialogs = dialogs;
        _host = host;
        _restarter = restarter;

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
            OperationResult.SucceededRestartRequired or OperationResult.SucceededAppRestartRequired => NotificationSeverity.Warning, // R5.3, 004 R6.1: say it clearly
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
        }
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
        ((INotifyCollectionChanged)queue.Operations).CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmpty));
        queue.LogRequested += async (_, operation) => await ShowLogAsync(operation);
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
    public Task ShowLogAsync(PackageOperation operation) =>
        _dialogs.ShowLogAsync(operation.Title, _logs.Read(operation), _logs.Folder);
}
