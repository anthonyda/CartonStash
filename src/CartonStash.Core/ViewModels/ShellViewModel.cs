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

    public ShellViewModel(PackageOperationQueue queue, UpdatesViewModel updates, IDialogService dialogs, ISettings settings)
    {
        _queue = queue;
        _updates = updates;
        _dialogs = dialogs;

        queue.Completed += (_, operation) => ShowOutcome(operation);
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

    public void Notify(string title, string message, NotificationSeverity severity)
    {
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

    private void ShowOutcome(PackageOperation operation)
    {
        OnPropertyChanged(nameof(ActivityText));
        var severity = operation.Result switch
        {
            OperationResult.Succeeded => NotificationSeverity.Success,
            OperationResult.SucceededRestartRequired => NotificationSeverity.Warning, // R5.3: say it clearly
            OperationResult.Cancelled => NotificationSeverity.Informational,
            _ => NotificationSeverity.Error,
        };
        Notify($"{operation.Title}: {operation.StatusText}", operation.Message ?? "", severity);
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

/// <summary>The Activity pane (003): every operation this session with progress, Cancel and Retry.</summary>
public sealed partial class ActivityViewModel : ObservableObject
{
    private readonly PackageOperationQueue _queue;

    public ActivityViewModel(PackageOperationQueue queue)
    {
        _queue = queue;
        ((INotifyCollectionChanged)queue.Operations).CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmpty));
    }

    public System.Collections.ObjectModel.ObservableCollection<PackageOperation> Operations => _queue.Operations;

    public bool IsEmpty => _queue.Operations.Count == 0;

    [RelayCommand]
    private void ClearFinished() => _queue.ClearFinished();
}
