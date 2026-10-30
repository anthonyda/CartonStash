using System.Collections.ObjectModel;
using CartonStash.Core.Models;

namespace CartonStash.Core.Services;

/// <summary>
/// Every change to the system goes through this queue (spec 003): one operation at a time, in order (R4.1),
/// never the same package twice (R4.2). Create it and call its members on the UI thread; the worker's
/// continuations come back to that thread, so <see cref="Operations"/> is only touched there.
/// </summary>
public sealed class PackageOperationQueue(IPackageInstaller installer, IOperationLogStore? logs = null)
{
    private readonly Queue<PackageOperation> _pending = new();
    private PackageOperation? _running;
    private CancellationTokenSource? _runningCancellation;
    private Task _worker = Task.CompletedTask;
    private bool _isWorking;

    /// <summary>All operations this session, newest first (Activity pane).</summary>
    public ObservableCollection<PackageOperation> Operations { get; } = [];

    /// <summary>Raised when an operation is added (or retried), so rows can show its state.</summary>
    public event EventHandler<PackageOperation>? Enqueued;

    /// <summary>Raised when the user asks for an operation's log (009 R2.2).</summary>
    public event EventHandler<PackageOperation>? LogRequested;

    /// <summary>Raised when an operation ends, whatever the result.</summary>
    public event EventHandler<PackageOperation>? Completed;

    /// <summary>Raised once the queue has nothing left to run (004: one Installed reload per drain).</summary>
    public event EventHandler? Drained;

    public bool HasPendingWork => _running is not null || _pending.Count > 0;

    public int ActiveCount => (_running is null ? 0 : 1) + _pending.Count;

    /// <summary>The worker, for tests and the close guard to await.</summary>
    public Task Idle => _worker;

    /// <summary>
    /// The newest operation for this installed program (or package, for search results), if any. Matched by
    /// <see cref="PackageInfo.Key"/>: programs sharing a WinGet ID don't share operations (002 R6.5).
    /// </summary>
    public PackageOperation? Find(PackageInfo package)
    {
        for (var i = 0; i < Operations.Count; i++)
        {
            if (string.Equals(Operations[i].Package.Key, package.Key, StringComparison.OrdinalIgnoreCase))
            {
                return Operations[i];
            }
        }
        return null;
    }

    /// <summary>Queues an operation, or returns the one already active for this package (R4.2).</summary>
    public PackageOperation Enqueue(OperationKind kind, PackageInfo package, bool acceptAgreements = false)
    {
        if (Find(package) is { IsActive: true } active)
        {
            return active;
        }

        var operation = new PackageOperation(kind, package, acceptAgreements, Cancel, Retry, o => LogRequested?.Invoke(this, o));
        Operations.Insert(0, operation);
        Start(operation);
        return operation;
    }

    /// <summary>Queues several operations in order (004 Update selected / Update all).</summary>
    public IReadOnlyList<PackageOperation> EnqueueMany(OperationKind kind, IEnumerable<PackageInfo> packages) =>
        packages.Select(p => Enqueue(kind, p)).ToList();

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

        if (operation == _running)
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
                Drained?.Invoke(this, EventArgs.Empty);
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
        Start(operation);
    }

    private void Start(PackageOperation operation)
    {
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
        while (_pending.TryDequeue(out var operation))
        {
            _running = operation;
            operation.StartedAt = DateTimeOffset.Now;
            operation.LogFilePath = logs?.CreateLogPath(operation);
            using var cancellation = new CancellationTokenSource();
            _runningCancellation = cancellation;

            OperationOutcome outcome;
            try
            {
                // Progress<T> posts back to this (UI) thread.
                var progress = new Progress<OperationProgress>(p =>
                {
                    if (operation.IsActive)
                    {
                        operation.Phase = p.Phase;
                        operation.Percent = p.Percent;
                    }
                });
                outcome = await installer.RunAsync(operation.Kind, operation.Package, operation.AcceptAgreements, operation.LogFilePath, progress, cancellation.Token);
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

            _running = null;
            _runningCancellation = null;
            Finish(operation, outcome);
        }

        _isWorking = false;
        Drained?.Invoke(this, EventArgs.Empty);
    }

    private void Finish(PackageOperation operation, OperationOutcome outcome)
    {
        operation.Percent = null;
        operation.Phase = OperationPhase.Done;
        operation.Result = outcome.Result;
        operation.Message = outcome.Message;
        operation.Details = outcome.Details;
        operation.FinishedAt = DateTimeOffset.Now;
        logs?.Complete(operation);

        // Active operations stay first, then finished ones, newest first (009 R1.5).
        var firstFinished = Operations.Count(o => o.IsActive);
        var index = Operations.IndexOf(operation);
        if (index >= 0 && index != firstFinished)
        {
            Operations.Move(index, firstFinished);
        }
        Completed?.Invoke(this, operation);
    }
}
