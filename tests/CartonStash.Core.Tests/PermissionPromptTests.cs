using CartonStash.Core.Models;
using CartonStash.Core.Services;
using CartonStash.Core.ViewModels;

namespace CartonStash.Core.Tests;

/// <summary>Spec 018: say when Windows' permission prompt is waiting.</summary>
public class PermissionPromptTests
{
    private static readonly PackageInfo Seven = new("7zip.7zip", "7-Zip", "24.08", "25.01", "winget", UpdateAvailable: true);
    private static readonly PackageInfo Vlc = new("VideoLAN.VLC", "VLC media player", "3.0.20", "3.0.21", "winget", UpdateAvailable: true);
    private static readonly DateTimeOffset T0 = new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);

    private sealed class FakeProbe : IPermissionPromptProbe
    {
        public bool Open { get; set; }

        public int Calls { get; private set; }

        public bool IsOpen()
        {
            Calls++;
            return Open;
        }
    }

    private sealed class FakeNotifier : IPermissionNotifier
    {
        public int Shown { get; private set; }

        public int Removed { get; private set; }

        public void Show() => Shown++;

        public void Remove() => Removed++;
    }

    private static (ScriptedInstaller Installer, PackageOperationQueue Queue, FakeProbe Probe, FakeNotifier Notifier, PermissionPromptMonitor Monitor) Setup()
    {
        var installer = new ScriptedInstaller();
        var queue = new PackageOperationQueue(installer);
        var probe = new FakeProbe();
        var notifier = new FakeNotifier();
        return (installer, queue, probe, notifier, new PermissionPromptMonitor(queue, probe, notifier));
    }

    [Fact]
    public void The_queue_says_which_operation_it_runs_R1_1()
    {
        var (installer, queue, _, _, _) = Setup();
        var changes = 0;
        queue.RunningChanged += (_, _) => changes++;

        var operation = queue.Enqueue(OperationKind.Upgrade, Seven);

        Assert.Same(operation, queue.Running);
        installer.Complete(Seven.Id);
        Assert.Null(queue.Running);
        Assert.Equal(2, changes);
    }

    [Fact]
    public void Nothing_is_looked_for_without_a_running_operation_R1_6()
    {
        var (installer, queue, probe, _, monitor) = Setup();
        Assert.False(monitor.NeedsTicks);

        queue.Enqueue(OperationKind.Upgrade, Seven);
        Assert.True(monitor.NeedsTicks);

        installer.Complete(Seven.Id);
        Assert.False(monitor.NeedsTicks);
        monitor.Tick(T0); // a late tick
        Assert.Equal(0, probe.Calls);
    }

    [Fact]
    public void An_open_prompt_marks_the_running_operation_and_opens_the_banner_R1_1_R1_2()
    {
        var (installer, queue, probe, _, monitor) = Setup();
        var running = queue.Enqueue(OperationKind.Upgrade, Seven);
        var queued = queue.Enqueue(OperationKind.Upgrade, Vlc);
        installer.Report(Seven.Id, OperationPhase.Running);

        probe.Open = true;
        monitor.Tick(T0);

        Assert.True(monitor.IsWaiting);
        Assert.True(running.IsWaitingForPermission);
        Assert.Equal("Waiting for your permission…", running.StatusText);
        Assert.False(queued.IsWaitingForPermission);
        Assert.Equal("Queued", queued.StatusText);
    }

    [Fact]
    public void A_closed_prompt_gives_back_the_normal_progress_R1_3()
    {
        var (installer, queue, probe, _, monitor) = Setup();
        var running = queue.Enqueue(OperationKind.Upgrade, Seven);
        installer.Report(Seven.Id, OperationPhase.Running);
        probe.Open = true;
        monitor.Tick(T0);

        probe.Open = false;
        monitor.Tick(T0.AddSeconds(1));

        Assert.False(monitor.IsWaiting);
        Assert.False(running.IsWaitingForPermission);
        Assert.Equal("Updating…", running.StatusText);
    }

    [Fact]
    public void One_notification_after_a_minute_and_removed_when_the_prompt_closes_R1_4()
    {
        var (installer, queue, probe, notifier, monitor) = Setup();
        queue.Enqueue(OperationKind.Upgrade, Seven);
        installer.Report(Seven.Id, OperationPhase.Running);
        probe.Open = true;

        monitor.Tick(T0);
        monitor.Tick(T0.AddSeconds(59));
        Assert.Equal(0, notifier.Shown);

        monitor.Tick(T0.AddSeconds(60));
        monitor.Tick(T0.AddSeconds(120));
        Assert.Equal(1, notifier.Shown);

        probe.Open = false;
        monitor.Tick(T0.AddSeconds(121));
        Assert.Equal(1, notifier.Removed);
    }

    [Fact]
    public void A_prompt_that_opens_again_starts_a_new_minute_R1_4()
    {
        var (installer, queue, probe, notifier, monitor) = Setup();
        queue.Enqueue(OperationKind.Upgrade, Seven);
        installer.Report(Seven.Id, OperationPhase.Running);

        probe.Open = true;
        monitor.Tick(T0);
        probe.Open = false;
        monitor.Tick(T0.AddSeconds(40));
        probe.Open = true;
        monitor.Tick(T0.AddSeconds(70));
        monitor.Tick(T0.AddSeconds(129));
        Assert.Equal(0, notifier.Shown);
        Assert.Equal(0, notifier.Removed); // none was shown

        monitor.Tick(T0.AddSeconds(130));
        Assert.Equal(1, notifier.Shown);
    }

    [Fact]
    public void The_next_operation_takes_the_mark_while_the_prompt_stays_open()
    {
        var (installer, queue, probe, notifier, monitor) = Setup();
        var first = queue.Enqueue(OperationKind.Upgrade, Seven);
        var second = queue.Enqueue(OperationKind.Upgrade, Vlc);
        probe.Open = true; // another program's prompt, say (R1.7)
        monitor.Tick(T0);

        installer.Complete(Seven.Id);

        Assert.False(first.IsWaitingForPermission);
        Assert.Same(second, queue.Running);
        Assert.True(second.IsWaitingForPermission);
        monitor.Tick(T0.AddSeconds(60)); // the same prompt: the minute kept counting
        Assert.Equal(1, notifier.Shown);
    }

    [Fact]
    public void The_queue_ending_closes_everything_R1_3()
    {
        var (installer, queue, probe, notifier, monitor) = Setup();
        var operation = queue.Enqueue(OperationKind.Upgrade, Seven);
        probe.Open = true;
        monitor.Tick(T0);
        monitor.Tick(T0.AddMinutes(1));

        installer.Complete(Seven.Id, OperationResult.Cancelled, "Windows' permission prompt was declined.");

        Assert.False(monitor.IsWaiting);
        Assert.False(operation.IsWaitingForPermission);
        Assert.Equal("Cancelled", operation.StatusText);
        Assert.Equal(1, notifier.Removed);
    }

    [Fact]
    public void Closing_the_window_removes_the_notification()
    {
        var (_, queue, probe, notifier, monitor) = Setup();
        queue.Enqueue(OperationKind.Upgrade, Seven);
        probe.Open = true;
        monitor.Tick(T0);
        monitor.Tick(T0.AddMinutes(1));

        monitor.Stop();

        Assert.Equal(1, notifier.Removed);
        Assert.False(monitor.NeedsTicks);
    }

    [Fact]
    public void The_may_ask_line_hides_while_the_waiting_text_shows_R1_9()
    {
        var (installer, queue, probe, _, monitor) = Setup();
        var operation = queue.Enqueue(OperationKind.Upgrade, Seven);
        installer.ReportPermission(Seven.Id);
        installer.Report(Seven.Id, OperationPhase.Running);
        Assert.True(operation.ShowPermission);

        probe.Open = true;
        monitor.Tick(T0);
        Assert.False(operation.ShowPermission);

        probe.Open = false;
        monitor.Tick(T0.AddSeconds(1));
        Assert.True(operation.ShowPermission);
    }

    [Fact]
    public void The_shell_banner_follows_the_monitor_R1_1()
    {
        var app = new TestApp();
        var probe = new FakeProbe();
        var monitor = new PermissionPromptMonitor(app.Queue, probe);
        var updates = new UpdatesViewModel(app.Store, app.Actions, app.Packages, app.Settings, app.ReleaseNotes);
        var shell = new ShellViewModel(app.Queue, updates, app.Dialogs, new FakeSettings(), permission: monitor);
        var queue = app.Queue;
        queue.Enqueue(OperationKind.Upgrade, Seven);

        probe.Open = true;
        monitor.Tick(T0);
        Assert.True(shell.IsPermissionBannerOpen);

        probe.Open = false;
        monitor.Tick(T0.AddSeconds(1));
        Assert.False(shell.IsPermissionBannerOpen);
    }
}
