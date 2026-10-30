using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CartonStash.Core.Models;

public enum OperationKind
{
    Install,
    Uninstall,
    Upgrade,
}

/// <summary>Where an operation is (003 R3.1). WinGet's own states are mapped onto these.</summary>
public enum OperationPhase
{
    Queued,
    Downloading,
    Running,
    Finishing,
    Done,
}

public enum OperationResult
{
    Succeeded,
    SucceededRestartRequired,
    Failed,
    Cancelled,
}

/// <summary>A progress report from the installer service. <paramref name="Percent"/> is 0–100, or null when unknown.</summary>
public readonly record struct OperationProgress(OperationPhase Phase, double? Percent = null);

/// <summary>What the Activity card shows as its state (009 R1.3).</summary>
public enum OperationState
{
    Queued,
    Running,
    Succeeded,
    RestartRequired,
    Failed,
    Cancelled,
}

/// <summary>How an operation ended, with a message the user can read (003 R5.2).</summary>
/// <param name="Details">Technical lines for the log (WinGet status, exit code, extended error), 009 R2.2.</param>
public sealed record OperationOutcome(OperationResult Result, string Message, string? Details = null)
{
    public bool IsSuccess => Result is OperationResult.Succeeded or OperationResult.SucceededRestartRequired;
}

/// <summary>
/// One install / uninstall / upgrade, as shown in a row and in the Activity pane (spec 003).
/// Created and driven by <see cref="Services.PackageOperationQueue"/>; all changes happen on the UI thread.
/// </summary>
public sealed partial class PackageOperation : ObservableObject
{
    private readonly Action<PackageOperation> _cancel;
    private readonly Action<PackageOperation> _retry;
    private readonly Action<PackageOperation> _showLog;

    internal PackageOperation(OperationKind kind, PackageInfo package, bool acceptAgreements,
        Action<PackageOperation> cancel, Action<PackageOperation> retry, Action<PackageOperation> showLog)
    {
        Kind = kind;
        Package = package;
        AcceptAgreements = acceptAgreements;
        _cancel = cancel;
        _retry = retry;
        _showLog = showLog;
    }

    public OperationKind Kind { get; }

    /// <summary>"Update", "Install" or "Uninstall", shown on the Activity card (009 R1.2).</summary>
    public string KindText => Verb;

    public PackageInfo Package { get; }

    /// <summary>The user already accepted the package's agreements (003 R1.3).</summary>
    public bool AcceptAgreements { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActive), nameof(CanCancel), nameof(StatusText), nameof(IsIndeterminate), nameof(State), nameof(HasLog))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand), nameof(RetryCommand), nameof(ShowLogCommand))]
    public partial OperationPhase Phase { get; internal set; }

    /// <summary>When the operation was queued, then when it really started (009 R1.4).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TimeText))]
    public partial DateTimeOffset? StartedAt { get; internal set; }

    [ObservableProperty]
    public partial DateTimeOffset? FinishedAt { get; internal set; }

    /// <summary>The log file of the latest run; null if none could be written (009 R2.5).</summary>
    public string? LogFilePath { get; internal set; }

    /// <summary>Technical result lines for the log (009 R2.2).</summary>
    public string? Details { get; internal set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText), nameof(IsIndeterminate), nameof(ProgressValue))]
    public partial double? Percent { get; internal set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText), nameof(IsSuccess), nameof(IsFailure), nameof(CanRetry), nameof(State))]
    [NotifyCanExecuteChangedFor(nameof(RetryCommand))]
    public partial OperationResult? Result { get; internal set; }

    /// <summary>The outcome message once finished (success, failure reason, restart note).</summary>
    [ObservableProperty]
    public partial string? Message { get; internal set; }

    public bool IsActive => Phase != OperationPhase.Done;

    /// <summary>The Log button is on finished cards only (009 R2.1).</summary>
    public bool HasLog => !IsActive;

    /// <summary>The card's state, for its icon and for the wording (009 R1.3).</summary>
    public OperationState State => (Phase, Result) switch
    {
        (OperationPhase.Queued, _) => OperationState.Queued,
        (not OperationPhase.Done, _) => OperationState.Running,
        (_, OperationResult.Succeeded) => OperationState.Succeeded,
        (_, OperationResult.SucceededRestartRequired) => OperationState.RestartRequired,
        (_, OperationResult.Cancelled) => OperationState.Cancelled,
        _ => OperationState.Failed,
    };

    /// <summary>Start time for the card (009 R1.4).</summary>
    public string TimeText => FormatTime(StartedAt, DateTimeOffset.Now);

    /// <summary>"14:32" today, with the date on other days.</summary>
    internal static string FormatTime(DateTimeOffset? time, DateTimeOffset now)
    {
        if (time is not { } t)
        {
            return "";
        }
        var local = t.ToLocalTime();
        return local.Date == now.ToLocalTime().Date ? local.ToString("t") : local.ToString("g");
    }

    /// <summary>WinGet can only stop an operation before the installer itself runs (003 R3.2).</summary>
    public bool CanCancel => Phase is OperationPhase.Queued or OperationPhase.Downloading;

    public bool IsSuccess => Result is OperationResult.Succeeded or OperationResult.SucceededRestartRequired;

    public bool IsFailure => Result is OperationResult.Failed;

    public bool CanRetry => Result is OperationResult.Failed or OperationResult.Cancelled;

    public bool IsIndeterminate => Phase != OperationPhase.Downloading || Percent is null;

    public double ProgressValue => Percent ?? 0;

    public string Title => $"{Verb} {Package.Name}";

    /// <summary>Short status for rows and the Activity pane (003 R3.1).</summary>
    public string StatusText => (Phase, Result) switch
    {
        (OperationPhase.Queued, _) => "Queued",
        (OperationPhase.Downloading, _) => Percent is { } p ? $"Downloading {p:0}%" : "Downloading…",
        (OperationPhase.Running, _) => Kind switch
        {
            OperationKind.Uninstall => "Uninstalling…",
            OperationKind.Upgrade => "Updating…",
            _ => "Installing…",
        },
        (OperationPhase.Finishing, _) => "Finishing…",
        (_, OperationResult.Succeeded) => DoneText,
        (_, OperationResult.SucceededRestartRequired) => $"{DoneText} · restart required",
        (_, OperationResult.Cancelled) => "Cancelled",
        _ => "Failed",
    };

    private string Verb => Kind switch
    {
        OperationKind.Uninstall => "Uninstall",
        OperationKind.Upgrade => "Update",
        _ => "Install",
    };

    private string DoneText => Kind switch
    {
        OperationKind.Uninstall => "Uninstalled",
        OperationKind.Upgrade => "Updated",
        _ => "Installed",
    };

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel() => _cancel(this);

    [RelayCommand(CanExecute = nameof(CanRetry))]
    private void Retry() => _retry(this);

    [RelayCommand(CanExecute = nameof(HasLog))]
    private void ShowLog() => _showLog(this);
}
