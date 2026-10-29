using WinGetFrontend.Core.Models;
using WinGetFrontend.Core.Services;

namespace WinGetFrontend.Core.Tests;

/// <summary>Spec 003 (and 004 EnqueueMany).</summary>
public class PackageOperationQueueTests
{
    [Fact]
    public void Runs_one_at_a_time_in_order() // R4.1
    {
        var app = new TestApp();

        var a = app.Queue.Enqueue(OperationKind.Install, Pkg.Remote("A"));
        var b = app.Queue.Enqueue(OperationKind.Install, Pkg.Remote("B"));

        Assert.Equal(["A"], app.Installer.Calls.Select(c => c.Id));
        Assert.Equal(OperationPhase.Queued, b.Phase);

        app.Installer.Complete("A");

        Assert.Equal(OperationResult.Succeeded, a.Result);
        Assert.Equal(["A", "B"], app.Installer.Calls.Select(c => c.Id));
        Assert.True(app.Installer.IsRunning("B"));
    }

    [Fact]
    public void Same_package_is_not_queued_twice() // R4.2
    {
        var app = new TestApp();

        var first = app.Queue.Enqueue(OperationKind.Install, Pkg.Remote("A"));
        var second = app.Queue.Enqueue(OperationKind.Install, Pkg.Remote("A"));

        Assert.Same(first, second);
        Assert.Single(app.Queue.Operations);
    }

    [Fact]
    public void Progress_updates_phase_and_percent() // R3.1
    {
        var app = new TestApp();
        var op = app.Queue.Enqueue(OperationKind.Install, Pkg.Remote("A"));

        app.Installer.Report("A", OperationPhase.Downloading, 42);
        Assert.Equal("Downloading 42%", op.StatusText);
        Assert.False(op.IsIndeterminate);

        app.Installer.Report("A", OperationPhase.Running);
        Assert.Equal("Installing…", op.StatusText);
        Assert.True(op.IsIndeterminate);
    }

    [Fact]
    public void Cancel_while_queued_removes_it_without_running() // R3.2
    {
        var app = new TestApp();
        app.Queue.Enqueue(OperationKind.Install, Pkg.Remote("A"));
        var b = app.Queue.Enqueue(OperationKind.Install, Pkg.Remote("B"));

        b.CancelCommand.Execute(null);
        app.Installer.Complete("A");

        Assert.Equal(OperationResult.Cancelled, b.Result);
        Assert.DoesNotContain(app.Installer.Calls, c => c.Id == "B");
        Assert.False(app.Queue.HasPendingWork);
    }

    [Fact]
    public async Task Cancel_while_downloading_cancels_the_winget_call() // R3.2
    {
        var app = new TestApp();
        var op = app.Queue.Enqueue(OperationKind.Install, Pkg.Remote("A"));
        app.Installer.Report("A", OperationPhase.Downloading, 10);

        op.CancelCommand.Execute(null);
        await app.Queue.Idle;

        Assert.Equal(OperationResult.Cancelled, op.Result);
        Assert.False(op.IsActive);
    }

    [Fact]
    public void Cancel_is_unavailable_once_the_installer_runs() // R3.2
    {
        var app = new TestApp();
        var op = app.Queue.Enqueue(OperationKind.Install, Pkg.Remote("A"));
        app.Installer.Report("A", OperationPhase.Running);

        Assert.False(op.CanCancel);
        Assert.False(op.CancelCommand.CanExecute(null));
        app.Queue.Cancel(op);

        Assert.True(app.Installer.IsRunning("A"));
    }

    [Fact]
    public void Failure_keeps_the_reason_and_can_be_retried() // R5.2
    {
        var app = new TestApp();
        var op = app.Queue.Enqueue(OperationKind.Install, Pkg.Remote("A"));

        app.Installer.Complete("A", OperationResult.Failed, "The installer failed with error code 1603.");

        Assert.True(op.IsFailure);
        Assert.Equal("The installer failed with error code 1603.", op.Message);
        Assert.Equal("Failed", op.StatusText);
        Assert.True(op.RetryCommand.CanExecute(null));

        op.RetryCommand.Execute(null);

        Assert.True(op.IsActive);
        Assert.Null(op.Result);
        Assert.Equal(2, app.Installer.Calls.Count);
        Assert.Single(app.Queue.Operations);
    }

    [Fact]
    public void Unexpected_exception_becomes_a_readable_failure() // R5.2
    {
        var app = new TestApp();
        var op = app.Queue.Enqueue(OperationKind.Install, Pkg.Remote("A"));

        app.Installer.Throw("A", new WinGetException("WinGet can no longer find the package 'A'.", null));

        Assert.Equal(OperationResult.Failed, op.Result);
        Assert.Equal("WinGet can no longer find the package 'A'.", op.Message);
    }

    [Fact]
    public void Restart_required_is_reported() // R5.3
    {
        var app = new TestApp();
        var op = app.Queue.Enqueue(OperationKind.Install, Pkg.Remote("A"));

        app.Installer.Complete("A", OperationResult.SucceededRestartRequired, "Installed. Restart your PC to finish.");

        Assert.True(op.IsSuccess);
        Assert.Equal("Installed · restart required", op.StatusText);
    }

    [Fact]
    public void Completed_and_drained_are_raised()
    {
        var app = new TestApp();
        var completed = new List<string>();
        var drained = 0;
        app.Queue.Completed += (_, o) => completed.Add(o.Package.Id);
        app.Queue.Drained += (_, _) => drained++;

        app.Queue.EnqueueMany(OperationKind.Upgrade, [Pkg.Installed("A"), Pkg.Installed("B")]);
        app.Installer.Complete("A");
        Assert.Equal(0, drained);
        app.Installer.Complete("B");

        Assert.Equal(["A", "B"], completed);
        Assert.Equal(1, drained);
        Assert.All(app.Installer.Calls, c => Assert.Equal(OperationKind.Upgrade, c.Kind));
    }

    [Fact]
    public void Enqueue_from_a_completed_handler_does_not_start_a_second_worker()
    {
        var app = new TestApp();
        app.Queue.Completed += (_, o) =>
        {
            if (o.Package.Id == "A")
            {
                app.Queue.Enqueue(OperationKind.Install, Pkg.Remote("C"));
            }
        };
        app.Queue.Enqueue(OperationKind.Install, Pkg.Remote("A"));
        app.Queue.Enqueue(OperationKind.Install, Pkg.Remote("B"));

        app.Installer.Complete("A");

        Assert.True(app.Installer.IsRunning("B"));
        Assert.False(app.Installer.IsRunning("C"));
    }

    [Fact]
    public void Clear_finished_keeps_active_ones()
    {
        var app = new TestApp();
        app.Queue.Enqueue(OperationKind.Install, Pkg.Remote("A"));
        app.Queue.Enqueue(OperationKind.Install, Pkg.Remote("B"));
        app.Installer.Complete("A");

        app.Queue.ClearFinished();

        Assert.Equal(["B"], app.Queue.Operations.Select(o => o.Package.Id));
    }
}
