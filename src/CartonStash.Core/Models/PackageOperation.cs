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

    /// <summary>App Installer updated, but WinGet runs from it: the new version starts with CartonStash (004 R6.1).</summary>
    SucceededAppRestartRequired,

    /// <summary>WinGet reported success for an update, but the old version is still installed and still has an update (013 R2.2).</summary>
    SucceededUnconfirmed,
    Failed,
    Cancelled,
}

/// <summary>A progress report from the installer service. <paramref name="Percent"/> is 0–100, or null when unknown.</summary>
public readonly record struct OperationProgress(OperationPhase Phase, double? Percent = null);

/// <summary>How WinGet ran the installer or uninstaller (003 R8.5).</summary>
public enum OperationMode
{
    /// <summary>WinGet's Silent mode (msiexec <c>/quiet</c>): no window, no consent prompt from Windows Installer.</summary>
    Silent,

    /// <summary>WinGet's Default mode (msiexec <c>/passive</c>), for a package installed for all users by Windows Installer (R8.1).</summary>
    DefaultForAllUsersMsi,
}

/// <summary>What the Activity card shows as its state (009 R1.3).</summary>
public enum OperationState
{
    Queued,
    Running,
    Succeeded,
    RestartRequired,

    /// <summary>An update WinGet reported as done, but the old version is still listed (013 R2.2).</summary>
    Unconfirmed,
    Failed,
    Cancelled,
}

/// <summary>
/// What an operation does beyond its kind (spec 014). <see cref="Default"/> is every operation before 014: WinGet's
/// default version, nothing after it.
/// </summary>
public sealed record OperationOptions
{
    public static OperationOptions Default { get; } = new();

    /// <summary>The version the user chose; null for WinGet's default (014 R2.1, R2.4).</summary>
    public string? Version { get; init; }

    /// <summary>Leave the package out of update checks once the operation succeeds (014 R3.2).</summary>
    public bool ExcludeOnSuccess { get; init; }

    /// <summary>An install of an older (or uncomparable) version over the installed one (014 R3).</summary>
    public bool GoingBack { get; init; }

    /// <summary>The version installed before going back, for Install {previous version} (014 R3.7).</summary>
    public string? PreviousVersion { get; init; }

    /// <summary>On an uninstall: the version to install once it succeeded (014 R3.5).</summary>
    public string? ThenInstall { get; init; }

    /// <summary>An install that follows the uninstall of R3.5: if it fails, the package is gone (014 R3.7).</summary>
    public bool AfterUninstall { get; init; }

    /// <summary>
    /// The scope the package was installed with before the R3.5 uninstall, for the install after it and for Install
    /// {previous version}: once it's uninstalled, nothing knows it (014 R3.8). Null to let the installer decide.
    /// </summary>
    public InstallScope? PreviousScope { get; init; }
}

/// <summary>The buttons a finished card may offer after going back failed (014 R3.5, R3.7).</summary>
public enum OperationFollowUp
{
    UninstallThenInstall,
    InstallPrevious,
}

/// <summary>How an operation ended, with a message the user can read (003 R5.2).</summary>
/// <param name="Details">Technical lines for the log (WinGet status, exit code, extended error), 009 R2.2.</param>
public sealed record OperationOutcome(OperationResult Result, string Message, string? Details = null)
{
    public bool IsSuccess => Result is OperationResult.Succeeded or OperationResult.SucceededRestartRequired or OperationResult.SucceededAppRestartRequired
        or OperationResult.SucceededUnconfirmed;

    /// <summary>False when retrying would fail the same way, so a failure offers no Retry (013 R3.1).</summary>
    public bool Retryable { get; init; } = true;

    /// <summary>Going back failed at the installer: the card offers Uninstall, then install (014 R3.5).</summary>
    public bool OffersUninstallFirst { get; init; }

    /// <summary>The scope the installer asked WinGet for (014 R3.8); <see cref="InstallScope.Any"/> lets WinGet decide.</summary>
    public InstallScope Scope { get; init; }

    /// <summary>True when <see cref="Scope"/> is how the package is installed, false when it's the setting (014 R3.8).</summary>
    public bool ScopeFromInstalled { get; init; }

    /// <summary>How WinGet ran it (003 R8.5).</summary>
    public OperationMode Mode { get; init; }
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
    private readonly Action _restartApp;
    private readonly Action<PackageOperation, OperationFollowUp> _followUp;

    internal PackageOperation(OperationKind kind, PackageInfo package, bool acceptAgreements,
        Action<PackageOperation> cancel, Action<PackageOperation> retry, Action<PackageOperation> showLog, Action restartApp,
        OperationOptions? options = null, Action<PackageOperation, OperationFollowUp>? followUp = null)
    {
        _followUp = followUp ?? ((_, _) => { });
        Options = options ?? OperationOptions.Default;
        _restartApp = restartApp;
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

    /// <summary>The chosen version and what follows the operation (014). Retry keeps them (R2.3).</summary>
    public OperationOptions Options { get; }

    /// <summary>The version the user chose, or null for WinGet's default (014 R2.1).</summary>
    public string? Version => Options.Version;

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

    /// <summary>From the outcome: false when Retry would fail the same way (013 R3.1). Set before <see cref="Result"/>.</summary>
    public bool Retryable { get; internal set; } = true;

    /// <summary>From the outcome: going back failed at the installer (014 R3.5). Set before <see cref="Result"/>.</summary>
    public bool OffersUninstallFirst { get; internal set; }

    /// <summary>From the outcome: the scope asked for, and whether it's the installed one (014 R3.8).</summary>
    public InstallScope Scope { get; internal set; }

    public bool ScopeFromInstalled { get; internal set; }

    /// <summary>From the outcome: how WinGet ran it (003 R8.5).</summary>
    public OperationMode Mode { get; internal set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText), nameof(IsIndeterminate), nameof(ProgressValue))]
    public partial double? Percent { get; internal set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText), nameof(IsSuccess), nameof(IsFailure), nameof(CanRetry), nameof(State), nameof(CanRestartApp),
        nameof(CanUninstallThenInstall), nameof(CanInstallPrevious))]
    [NotifyCanExecuteChangedFor(nameof(RetryCommand))]
    public partial OperationResult? Result { get; internal set; }

    /// <summary>The outcome message once finished (success, failure reason, restart note).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string? Message { get; internal set; }

    /// <summary>A plain bool for the card: x:Bind doesn't call a function again once its argument is null (011 T8).</summary>
    public bool HasMessage => Message is not null;

    public bool IsActive => Phase != OperationPhase.Done;

    /// <summary>The Log button is on finished cards only (009 R2.1).</summary>
    public bool HasLog => !IsActive;

    /// <summary>The card's state, for its icon and for the wording (009 R1.3).</summary>
    public OperationState State => (Phase, Result) switch
    {
        (OperationPhase.Queued, _) => OperationState.Queued,
        (not OperationPhase.Done, _) => OperationState.Running,
        (_, OperationResult.Succeeded) => OperationState.Succeeded,
        (_, OperationResult.SucceededRestartRequired or OperationResult.SucceededAppRestartRequired) => OperationState.RestartRequired,
        (_, OperationResult.SucceededUnconfirmed) => OperationState.Unconfirmed,
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

    public bool IsSuccess => Result is OperationResult.Succeeded or OperationResult.SucceededRestartRequired or OperationResult.SucceededAppRestartRequired
        or OperationResult.SucceededUnconfirmed;

    public bool IsFailure => Result is OperationResult.Failed;

    // No Retry on a warning, and none where WinGet would refuse the same way again (013 R2.2, R3.1).
    public bool CanRetry => (Result is OperationResult.Failed && Retryable) || Result is OperationResult.Cancelled;

    /// <summary>014 R3.5: going back failed at the installer, not because the program was running (003 R7).</summary>
    public bool CanUninstallThenInstall => Result is OperationResult.Failed && Options.GoingBack && OffersUninstallFirst && Version is not null;

    /// <summary>"Uninstall, then install 2.56.0.3".</summary>
    public string UninstallThenInstallText => $"Uninstall, then install {Version}";

    /// <summary>The button's accessible name, with the package (Q5).</summary>
    public string UninstallThenInstallName => $"Uninstall {Package.Name}, then install {Version}";

    /// <summary>014 R3.7: the install after the uninstall didn't happen, so the package is gone.</summary>
    public bool CanInstallPrevious => Result is OperationResult.Failed or OperationResult.Cancelled
        && Options.AfterUninstall && Options.PreviousVersion is not null;

    /// <summary>"Install 2.56.0.4".</summary>
    public string InstallPreviousText => $"Install {Options.PreviousVersion}";

    /// <summary>The button's accessible name, with the package (Q5).</summary>
    public string InstallPreviousName => $"Install {Package.Name} {Options.PreviousVersion}";

    /// <summary>The card offers Restart CartonStash (004 R6.3).</summary>
    public bool CanRestartApp => Result is OperationResult.SucceededAppRestartRequired;

    public bool IsIndeterminate => Phase != OperationPhase.Downloading || Percent is null;

    public double ProgressValue => Percent ?? 0;

    /// <summary>"Install PowerToys 0.101.0" when a version was chosen (014 R2.1).</summary>
    public string Title => Version is { } version ? $"{Verb} {Package.Name} {version}" : $"{Verb} {Package.Name}";

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
        (_, OperationResult.SucceededAppRestartRequired) => $"{DoneText} · restart CartonStash",
        (_, OperationResult.SucceededUnconfirmed) => "Update not confirmed",
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

    [RelayCommand]
    private void RestartApp() => _restartApp();

    [RelayCommand]
    private void UninstallThenInstall() => _followUp(this, OperationFollowUp.UninstallThenInstall);

    [RelayCommand]
    private void InstallPrevious() => _followUp(this, OperationFollowUp.InstallPrevious);
}
