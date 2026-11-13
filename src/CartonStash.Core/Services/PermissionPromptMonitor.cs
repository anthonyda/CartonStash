using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CartonStash.Core.Models;

namespace CartonStash.Core.Services;

/// <summary>Says whether Windows' permission (UAC) prompt is open (018 R1.6).</summary>
public interface IPermissionPromptProbe
{
    bool IsOpen();
}

/// <summary>
/// 018 R1.6, Q1: the prompt is open while Windows' consent process runs. An unelevated app sees its name in the process
/// list but can't open it, and this never tries (P1: read only).
/// </summary>
public sealed class ConsentProcessProbe : IPermissionPromptProbe
{
    public bool IsOpen()
    {
        try
        {
            var processes = Process.GetProcessesByName("consent");
            foreach (var process in processes)
            {
                process.Dispose();
            }
            return processes.Length > 0;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or UnauthorizedAccessException)
        {
            return false; // tried again at the next tick
        }
    }
}

/// <summary>The Windows notification of 018 R1.4.</summary>
public interface IPermissionNotifier
{
    void Show();

    /// <summary>Takes it out of the notification center. Does nothing when none is shown.</summary>
    void Remove();
}

/// <summary>
/// Spec 018: while the queue runs an operation, looks for Windows' permission prompt on each <see cref="Tick"/> (the App
/// ticks once a second while <see cref="NeedsTicks"/>). Marks the running operation as waiting (R1.2), opens the banner
/// through <see cref="IsWaiting"/> (R1.1), and after a minute without a break shows one notification (R1.4). Use it on
/// the UI thread, like the queue.
/// </summary>
public sealed partial class PermissionPromptMonitor : ObservableObject
{
    /// <summary>R1.4: how long the prompt stays open before the notification.</summary>
    public static readonly TimeSpan NotifyAfter = TimeSpan.FromMinutes(1);

    private readonly PackageOperationQueue _queue;
    private readonly IPermissionPromptProbe _probe;
    private readonly IPermissionNotifier? _notifier;
    private PackageOperation? _flagged;
    private DateTimeOffset? _openedAt;
    private bool _notified;

    public PermissionPromptMonitor(PackageOperationQueue queue, IPermissionPromptProbe probe, IPermissionNotifier? notifier = null)
    {
        _queue = queue;
        _probe = probe;
        _notifier = notifier;
        queue.RunningChanged += (_, _) => OnRunningChanged();
    }

    /// <summary>R1.6: true only while the queue runs an operation; the App's timer runs only then.</summary>
    [ObservableProperty]
    public partial bool NeedsTicks { get; private set; }

    /// <summary>R1.1: the prompt is open while an operation runs. The banner shows while this is true.</summary>
    [ObservableProperty]
    public partial bool IsWaiting { get; private set; }

    /// <summary>One look at the process list, at most once a second (R1.6).</summary>
    public void Tick(DateTimeOffset now)
    {
        if (_queue.Running is not { } running)
        {
            if (!_queue.HasPendingWork)
            {
                Close();
            }
            return; // between two operations: the next one decides
        }

        if (!_probe.IsOpen())
        {
            Close(); // R1.3
            return;
        }

        _openedAt ??= now;
        Flag(running);
        IsWaiting = true;
        if (!_notified && now - _openedAt >= NotifyAfter)
        {
            _notified = true; // R1.4: one per prompt
            _notifier?.Show();
        }
    }

    /// <summary>The window closes: no notification stays behind it (design, edge cases).</summary>
    public void Stop()
    {
        Close();
        NeedsTicks = false;
    }

    private void OnRunningChanged()
    {
        // Between two operations the queue has none running for a moment; the next one follows at once, so the prompt
        // (if still open) stays the same one and its minute keeps counting.
        NeedsTicks = _queue.HasPendingWork;
        if (_queue.Running is { } running)
        {
            if (IsWaiting)
            {
                Flag(running);
            }
        }
        else if (_queue.HasPendingWork)
        {
            Unflag(); // the finished operation isn't waiting any more
        }
        else
        {
            Close();
        }
    }

    private void Flag(PackageOperation operation)
    {
        if (_flagged == operation)
        {
            return;
        }
        if (_flagged is not null)
        {
            _flagged.IsWaitingForPermission = false;
        }
        _flagged = operation;
        operation.IsWaitingForPermission = true;
        operation.SawPermissionPrompt = true; // R2.1
    }

    private void Unflag()
    {
        if (_flagged is not null)
        {
            _flagged.IsWaitingForPermission = false;
            _flagged = null;
        }
    }

    private void Close()
    {
        Unflag();
        IsWaiting = false;
        _openedAt = null;
        if (_notified)
        {
            _notified = false; // R1.4: a prompt that opens again starts a new minute
            _notifier?.Remove();
        }
    }
}
