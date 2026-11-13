using System.Collections.ObjectModel;
using CartonStash.Core.Models;

namespace CartonStash.Core.Services;

/// <summary>
/// Every change to the system goes through this queue (spec 003): one operation at a time, in order (R4.1),
/// never the same package twice (R4.2). Create it and call its members on the UI thread; the worker's
/// continuations come back to that thread, so <see cref="Operations"/> is only touched there.
/// </summary>
public sealed class PackageOperationQueue(IPackageInstaller installer, IOperationLogStore? logs = null, IElevatedRunner? elevated = null)
{
    private readonly Queue<PackageOperation> _pending = new();
    /// <summary>The operation the worker took off the waiting list, whatever its phase; null between operations (018 R1.1).</summary>
    public PackageOperation? Running
    {
        get;
        private set
        {
            if (field != value)
            {
                field = value;
                Raise(RunningChanged, EventArgs.Empty);
            }
        }
    }

    /// <summary>Raised when <see cref="Running"/> changes (018 R1.6: the permission prompt is only looked for while it's set).</summary>
    public event EventHandler? RunningChanged;
    private CancellationTokenSource? _runningCancellation;
    private Task _worker = Task.CompletedTask;
    private bool _isWorking;
    private long _sequence;

    /// <summary>All operations this session, newest first (Activity pane).</summary>
    public ObservableCollection<PackageOperation> Operations { get; } = [];

    /// <summary>Raised when an operation is added (or retried), so rows can show its state.</summary>
    public event EventHandler<PackageOperation>? Enqueued;

    /// <summary>Raised when the user asks for an operation's log (009 R2.2).</summary>
    public event EventHandler<PackageOperation>? LogRequested;

    /// <summary>Raised when the user asks to restart CartonStash to finish App Installer's update (004 R6.3).</summary>
    public event EventHandler? RestartRequested;

    /// <summary>A card's Uninstall, then install or Install {previous version} (014 R3.5, R3.7); PackageActions handles it.</summary>
    public event EventHandler<(PackageOperation Operation, OperationFollowUp FollowUp)>? FollowUpRequested;

    /// <summary>Raised when an operation ends, whatever the result.</summary>
    public event EventHandler<PackageOperation>? Completed;

    /// <summary>Raised once the queue has nothing left to run (004: one Installed reload per drain).</summary>
    public event EventHandler? Drained;

    public bool HasPendingWork => Running is not null || _pending.Count > 0;

    public int ActiveCount => (Running is null ? 0 : 1) + _pending.Count;

    /// <summary>The worker, for tests and the close guard to await.</summary>
    public Task Idle => _worker;

    /// <summary>
    /// The newest operation for this installed program (or package, for search results), if any. Matched by
    /// <see cref="PackageInfo.IsSamePackage"/>: programs sharing a WinGet ID don't share operations (002 R6.5), and a
    /// Search row sees one queued from Installed or Updates (BR-07).
    /// </summary>
    public PackageOperation? Find(PackageInfo package)
    {
        for (var i = 0; i < Operations.Count; i++)
        {
            if (Operations[i].Package.IsSamePackage(package)) // BR-07
            {
                return Operations[i];
            }
        }
        return null;
    }

    /// <summary>Queues an operation, or returns the one already active for this package (R4.2).</summary>
    public PackageOperation Enqueue(OperationKind kind, PackageInfo package, bool acceptAgreements = false, OperationOptions? options = null)
    {
        if (Find(package) is { IsActive: true } active)
        {
            return active;
        }

        var operation = new PackageOperation(kind, package, acceptAgreements, Cancel, Retry, o => LogRequested?.Invoke(this, o), RequestRestart, options,
            (o, followUp) => FollowUpRequested?.Invoke(this, (o, followUp)));
        Operations.Insert(0, operation);
        Start(operation);
        return operation;
    }

    /// <summary>Queues several operations in order (004 Update selected / Update all).</summary>
    public IReadOnlyList<PackageOperation> EnqueueMany(OperationKind kind, IEnumerable<PackageInfo> packages) =>
        packages.Select(p => Enqueue(kind, p)).ToList();

    /// <summary>The Restart CartonStash buttons of a card or a row (004 R6.3).</summary>
    public void RequestRestart() => RestartRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Removes finished operations from the Activity list.</summary>
    public void ClearFinished()
    {
        for (var i = Operations.Count - 1; i >= 0; i--)
        {
            if (!Operations[i].IsActive)
            {
                Operations.RemoveAt(i);
            }
        }
    }

    /// <summary>Cancels a queued or downloading operation (R3.2). Does nothing once the installer runs.</summary>
    public void Cancel(PackageOperation operation)
    {
        if (!operation.CanCancel)
        {
            return;
        }

        if (operation == Running)
        {
            _runningCancellation?.Cancel();
            return;
        }

        if (_pending.Contains(operation))
        {
            var rest = _pending.Where(o => o != operation).ToList();
            _pending.Clear();
            foreach (var o in rest)
            {
                _pending.Enqueue(o);
            }
            Finish(operation, new OperationOutcome(OperationResult.Cancelled, "Cancelled before it started."));
            if (!HasPendingWork)
            {
                Raise(Drained, EventArgs.Empty);
            }
        }
    }

    /// <summary>Runs a failed or cancelled operation again (R5.2).</summary>
    public void Retry(PackageOperation operation)
    {
        if (!operation.CanRetry || Find(operation.Package) is { IsActive: true })
        {
            return;
        }

        // Move it to the top of the Activity list as a fresh run.
        Operations.Remove(operation);
        Operations.Insert(0, operation);
        operation.Result = null;
        operation.Message = null;
        operation.Percent = null;
        operation.FinishedAt = null;
        operation.LogFilePath = null;
        operation.Details = null;
        operation.MayAskPermission = false; // Retry decides it again (003 R8.3)
        Start(operation);
    }

    private void Start(PackageOperation operation)
    {
        operation.Sequence = ++_sequence; // 009 R4.5: run order, set first: the next changes re-sort the Activity list
        operation.SawPermissionPrompt = false; // 018 R2.1: per run
        operation.Phase = OperationPhase.Queued;
        operation.StartedAt = DateTimeOffset.Now;
        _pending.Enqueue(operation);
        Enqueued?.Invoke(this, operation);
        if (!_isWorking)
        {
            _worker = RunWorkerAsync();
        }
    }

    private async Task RunWorkerAsync()
    {
        // Set before the first await: a Completed handler may enqueue while this loop runs synchronously.
        _isWorking = true;
        try
        {
            while (_pending.TryDequeue(out var operation))
            {
                try
                {
                    await RunOneAsync(operation);
                }
                catch (Exception ex)
                {
                    // BR-10 (009 R2.5): nothing around one operation stops the queue. It ends as failed if it hadn't ended.
                    if (operation.IsActive)
                    {
                        ApplyOutcome(operation, new OperationOutcome(OperationResult.Failed, $"Something went wrong: {ex.Message}"));
                        Announce(operation);
                    }
                }
                finally
                {
                    Running = null;
                    _runningCancellation = null;
                }
            }
        }
        finally
        {
            _isWorking = false;
        }
        Raise(Drained, EventArgs.Empty);
    }

    private async Task RunOneAsync(PackageOperation operation)
    {
        {
            Running = operation;
            operation.StartedAt = DateTimeOffset.Now;
            operation.LogFilePath = CreateLogPath(operation);
            using var cancellation = new CancellationTokenSource();
            _runningCancellation = cancellation;

            OperationOutcome outcome;
            try
            {
                // Progress<T> posts back to this (UI) thread.
                var progress = new Progress<OperationProgress>(p =>
                {
                    if (!operation.IsActive)
                    {
                        return;
                    }
                    if (p.MayAskPermission)
                    {
                        operation.MayAskPermission = true; // 003 R8.3: a notice, not a phase
                        return;
                    }
                    operation.Phase = p.Phase;
                    operation.Percent = p.Percent;
                });
                var (scope, scopeSource) = ExplicitScope(operation);
                if (operation.Options.Elevated && elevated is not null)
                {
                    // 017 R1.4, R1.5: another process runs it with administrator rights; Windows asks first. No percentage
                    // comes back from there.
                    operation.MayAskPermission = true;
                    operation.Phase = OperationPhase.Running;
                    outcome = await elevated.RunAsync(new ElevatedRequest(operation.Kind, operation.Package, operation.AcceptAgreements, operation.Version,
                        operation.LogFilePath, scope, scopeSource), cancellation.Token);
                }
                else
                {
                    outcome = await installer.RunAsync(operation.Kind, operation.Package, operation.AcceptAgreements, operation.Version, operation.LogFilePath, progress, cancellation.Token,
                        scope, scopeSource);
                }
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                outcome = new OperationOutcome(OperationResult.Cancelled, "Cancelled.");
            }
            catch (Exception ex)
            {
                outcome = new OperationOutcome(OperationResult.Failed,
                    ex is WinGetException ? ex.Message : $"Something went wrong: {ex.Message}");
            }

            Running = null;
            _runningCancellation = null;
            outcome = DeclinedPrompt(operation, outcome);
            ApplyOutcome(operation, outcome);
            if (logs is not null && operation.LogFilePath is not null)
            {
                // BR-20: an installer's log can be tens of MB; read and write it off the UI thread.
                var written = await Task.Run(() => CompleteLog(operation));
                if (!written)
                {
                    operation.LogFilePath = null; // the dialog falls back to the summary
                }
            }
            Announce(operation);
        }
    }

    private string? CreateLogPath(PackageOperation operation)
    {
        try
        {
            return logs?.CreateLogPath(operation);
        }
        catch (Exception)
        {
            return null; // 009 R2.5: logging never affects an operation
        }
    }

    private bool CompleteLog(PackageOperation operation)
    {
        try
        {
            return logs?.Complete(operation) ?? true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Raises an event to each handler in turn: one that throws doesn't stop the others or the queue (BR-10).</summary>
    /// <summary>
    /// 018 R2.1, R2.3: a result a declined prompt gives is Cancelled only when the prompt was seen during this run; 1625
    /// alone may be a real policy. WinGet's status and codes stay in <see cref="OperationOutcome.Details"/> (R2.4).
    /// </summary>
    internal static OperationOutcome DeclinedPrompt(PackageOperation operation, OperationOutcome outcome) =>
        outcome is { Result: OperationResult.Failed, MayBeDeclinedPrompt: true } && operation.SawPermissionPrompt
            ? outcome with
            {
                Result = OperationResult.Cancelled,
                Message = ElevatedProtocol.DeclinedMessage,
                OffersRetryAsAdmin = false, // R2.4: the user declined the rights
                OffersUninstallFirst = false,
            }
            : outcome;

    private void Raise<T>(EventHandler<T>? handlers, T args)
    {
        foreach (var handler in handlers?.GetInvocationList() ?? [])
        {
            try
            {
                ((EventHandler<T>)handler)(this, args);
            }
            catch (Exception)
            {
                // a handler's failure is its own (009 R2.5)
            }
        }
    }

    private void Raise(EventHandler? handlers, EventArgs args)
    {
        foreach (var handler in handlers?.GetInvocationList() ?? [])
        {
            try
            {
                ((EventHandler)handler)(this, args);
            }
            catch (Exception)
            {
            }
        }
    }

    private void Finish(PackageOperation operation, OperationOutcome outcome)
    {
        ApplyOutcome(operation, outcome);
        if (CompleteLog(operation) is false)
        {
            operation.LogFilePath = null;
        }
        Announce(operation);
    }

    private void ApplyOutcome(PackageOperation operation, OperationOutcome outcome)
    {
        outcome = AfterUninstallOutcome(outcome, operation);
        operation.Percent = null;
        operation.Phase = OperationPhase.Done;
        operation.Retryable = outcome.Retryable; // before Result, which notifies CanRetry
        operation.OffersUninstallFirst = outcome.OffersUninstallFirst;
        operation.OffersRetryAsAdmin = outcome.OffersRetryAsAdmin;
        operation.Scope = outcome.Scope;
        operation.ScopeSource = outcome.ScopeSource;
        operation.Mode = outcome.Mode;
        operation.Result = outcome.Result;
        operation.Message = outcome.Message;
        operation.Details = outcome.Details;
        operation.FinishedAt = DateTimeOffset.Now;
    }

    private void Announce(PackageOperation operation)
    {
        // Active operations stay first, then finished ones, newest first (009 R1.5).
        var firstFinished = Operations.Count(o => o.IsActive);
        var index = Operations.IndexOf(operation);
        if (index >= 0 && index != firstFinished)
        {
            Operations.Move(index, firstFinished);
        }
        Raise(Completed, operation);
    }

    /// <summary>
    /// The scope the installer must ask for, if any: the one the user chose (015 R1.5), else the installed one carried
    /// across the R3.5 uninstall (014 R3.8). An uninstall asks for none.
    /// </summary>
    internal static (InstallScope? Scope, ScopeSource Source) ExplicitScope(PackageOperation operation) =>
        operation.Kind == OperationKind.Uninstall ? (null, ScopeSource.Installed)
        : operation.Options.Scope is { } chosen ? (chosen, ScopeSource.Chosen)
        : (operation.Options.PreviousScope, ScopeSource.Installed);

    /// <summary>
    /// 014 R3.7: the install that follows the uninstall of R3.5 didn't happen, so the package is no longer installed. That
    /// comes first, then the reason.
    /// </summary>
    internal static OperationOutcome AfterUninstallOutcome(OperationOutcome outcome, PackageOperation operation) =>
        operation.Options.AfterUninstall && outcome.Result is OperationResult.Failed or OperationResult.Cancelled
            ? outcome with
            {
                Message = $"{operation.Package.Name} is no longer installed: it was uninstalled to make way for {operation.Version}, "
                    + $"and then that install didn't finish. {outcome.Message}",
            }
            : outcome;
}
