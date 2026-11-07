using CartonStash.Core.Models;
using CartonStash.Core.Services;
using CartonStash.Core.ViewModels;

namespace CartonStash.Core.Tests;

/// <summary>Spec 003 notifications and close guard; 004 badge.</summary>
public class ShellViewModelTests
{
    private static (TestApp App, ShellViewModel Shell, UpdatesViewModel Updates) Create(bool settingsReset = false)
    {
        var app = new TestApp();
        var updates = new UpdatesViewModel(app.Store, app.Actions, app.Packages, app.Settings, app.ReleaseNotes);
        var shell = new ShellViewModel(app.Queue, updates, app.Dialogs, new FakeSettings { WasReset = settingsReset });
        return (app, shell, updates);
    }

    private sealed class FakeRestarter(bool started = true) : IAppRestarter
    {
        public List<string?> Calls { get; } = [];

        public bool Restart(string? pendingVersion)
        {
            Calls.Add(pendingVersion);
            return started;
        }
    }

    private static (TestApp App, ShellViewModel Shell, FakeRestarter Restarter) CreateWithRestarter(bool started = true)
    {
        var app = new TestApp();
        var updates = new UpdatesViewModel(app.Store, app.Actions, app.Packages, app.Settings, app.ReleaseNotes);
        var restarter = new FakeRestarter(started);
        return (app, new ShellViewModel(app.Queue, updates, app.Dialogs, new FakeSettings(), app.Actions.Host, restarter), restarter);
    }

    [Fact]
    public void Restart_from_the_card_restarts_with_the_pending_version() // 004 R6.1, R6.3
    {
        var (app, shell, restarter) = CreateWithRestarter();
        var op = app.Queue.Enqueue(OperationKind.Upgrade, WinGetHostTests.AppInstaller());
        app.Installer.Complete("Microsoft.AppInstaller", OperationResult.SucceededAppRestartRequired, "Updated. Restart CartonStash to finish.");

        Assert.Equal(NotificationSeverity.Warning, shell.NotificationSeverity);
        Assert.True(shell.HasNotificationRestart);
        Assert.True(op.CanRestartApp);

        op.RestartAppCommand.Execute(null);

        Assert.Equal(["1.29.379.0"], restarter.Calls);
        Assert.Empty(app.Dialogs.Confirmations);
    }

    [Fact]
    public async Task Restart_with_pending_work_asks_first_and_respects_the_answer() // 004 R6.3, 003 R6.2
    {
        var (app, shell, restarter) = CreateWithRestarter();
        app.Queue.Enqueue(OperationKind.Install, Pkg.Remote("A"));

        app.Dialogs.Answer = false;
        await shell.RestartCommand.ExecuteAsync(null);
        Assert.Empty(restarter.Calls);

        app.Dialogs.Answer = true;
        await shell.RestartCommand.ExecuteAsync(null);
        Assert.Equal(2, app.Dialogs.Confirmations.Count);
        Assert.Single(restarter.Calls);
    }

    [Fact]
    public async Task Restart_waits_for_the_installed_list_to_finish_loading() // 004 R6.3, T15
    {
        var (app, shell, restarter) = CreateWithRestarter();
        var load = new TaskCompletionSource<IReadOnlyList<PackageInfo>>();
        app.Packages.Installed = _ => load.Task;
        var reload = app.Store.ReloadAsync();

        var restart = shell.RestartCommand.ExecuteAsync(null);
        await Task.Delay(300);
        Assert.Empty(restarter.Calls);

        load.SetResult([]);
        await reload;
        await restart;
        Assert.Single(restarter.Calls);
    }

    [Fact]
    public async Task Restart_that_fails_says_how_to_finish() // 004 R6.3
    {
        var (_, shell, _) = CreateWithRestarter(started: false);

        await shell.RestartCommand.ExecuteAsync(null);

        Assert.Equal(NotificationSeverity.Error, shell.NotificationSeverity);
        Assert.Contains("open it again", shell.NotificationMessage);
        Assert.False(shell.HasNotificationRestart);
    }

    [Fact]
    public async Task After_a_restart_the_new_version_needs_no_message() // 004 R6.4
    {
        var (app, shell, _) = CreateWithRestarter();
        app.SetInstalled(WinGetHostTests.AppInstaller("1.29.380.0") with { UpdateAvailable = false });

        await shell.FinishRestartAsync("1.29.379.0");

        Assert.False(shell.IsNotificationOpen);
        Assert.Null(app.Actions.Host.PendingVersion);
    }

    [Fact]
    public async Task After_a_restart_the_old_version_says_what_finishes_it() // 004 R6.4
    {
        var (app, shell, _) = CreateWithRestarter();
        app.SetInstalled(WinGetHostTests.AppInstaller());

        await shell.FinishRestartAsync("1.29.379.0");

        Assert.True(shell.IsNotificationOpen);
        Assert.Equal(NotificationSeverity.Warning, shell.NotificationSeverity);
        Assert.Contains("every program using WinGet is closed", shell.NotificationMessage);
        Assert.True(app.Actions.Host.IsPending(WinGetHostTests.AppInstaller()));
    }

    [Fact]
    public void Other_outcomes_have_no_restart_button()
    {
        var (app, shell, _) = CreateWithRestarter();
        var op = app.Queue.Enqueue(OperationKind.Install, Pkg.Remote("A"));
        app.Installer.Complete("A", OperationResult.SucceededRestartRequired, "Installed. Restart your PC to finish.");

        Assert.False(shell.HasNotificationRestart);
        Assert.False(op.CanRestartApp);
    }

    [Fact]
    public async Task Close_is_immediate_with_nothing_running() // R6.2
    {
        var (app, shell, _) = Create();

        Assert.True(await shell.ConfirmCloseAsync());
        Assert.Empty(app.Dialogs.Confirmations);
    }

    [Fact]
    public async Task Close_with_pending_work_warns_and_respects_the_answer() // R6.2
    {
        var (app, shell, _) = Create();
        app.Queue.Enqueue(OperationKind.Install, Pkg.Remote("A"));
        app.Queue.Enqueue(OperationKind.Install, Pkg.Remote("B"));

        app.Dialogs.Answer = false;
        Assert.False(await shell.ConfirmCloseAsync());
        app.Dialogs.Answer = true;
        Assert.True(await shell.ConfirmCloseAsync());

        Assert.Contains("2 operations are still running", app.Dialogs.Confirmations[0]);
    }

    [Fact]
    public void Success_and_failure_are_notified() // R5.1, R5.2
    {
        var (app, shell, _) = Create();

        app.Queue.Enqueue(OperationKind.Install, Pkg.Remote("A", "Contoso"));
        app.Installer.Complete("A");
        Assert.Equal(NotificationSeverity.Success, shell.NotificationSeverity);
        Assert.Equal("Install Contoso: Installed", shell.NotificationTitle);

        app.Queue.Enqueue(OperationKind.Uninstall, Pkg.Installed("B", "Bravo"));
        app.Installer.Complete("B", OperationResult.Failed, "The uninstaller failed with error code 5.");
        Assert.True(shell.IsNotificationOpen);
        Assert.Equal(NotificationSeverity.Error, shell.NotificationSeverity);
        Assert.Equal("The uninstaller failed with error code 5.", shell.NotificationMessage);
    }

    [Fact]
    public void An_unconfirmed_update_is_a_warning() // 013 R2.2
    {
        var (app, shell, _) = Create();

        app.Queue.Enqueue(OperationKind.Upgrade, Pkg.Installed("Autodesk.Fusion", update: true));
        app.Installer.Complete("Autodesk.Fusion", OperationResult.SucceededUnconfirmed, "Windows still lists Autodesk Fusion 2706.1.14.");

        Assert.Equal(NotificationSeverity.Warning, shell.NotificationSeverity);
        Assert.Contains("Update not confirmed", shell.NotificationTitle);
        Assert.DoesNotContain("successfully", shell.NotificationMessage);
    }

    [Fact]
    public void Restart_required_is_a_warning() // R5.3
    {
        var (app, shell, _) = Create();

        app.Queue.Enqueue(OperationKind.Install, Pkg.Remote("A"));
        app.Installer.Complete("A", OperationResult.SucceededRestartRequired, "Installed. Restart your PC to finish.");

        Assert.Equal(NotificationSeverity.Warning, shell.NotificationSeverity);
        Assert.Contains("Restart", shell.NotificationMessage);
    }

    [Fact]
    public void Activity_text_counts_active_operations()
    {
        var (app, shell, _) = Create();
        Assert.Equal("Activity", shell.ActivityText);

        app.Queue.Enqueue(OperationKind.Install, Pkg.Remote("A"));
        app.Queue.Enqueue(OperationKind.Install, Pkg.Remote("B"));

        Assert.Equal("Activity (2)", shell.ActivityText);
    }

    [Fact]
    public async Task Updates_badge_counts_once_loaded() // 004 R4.1
    {
        var (app, shell, _) = Create();
        app.SetInstalled(Pkg.Installed("A", update: true), Pkg.Installed("B", update: true), Pkg.Installed("C"));
        Assert.Equal(0, shell.UpdatesCount);

        await app.Store.EnsureLoadedAsync();

        Assert.Equal(2, shell.UpdatesCount);
        Assert.True(shell.HasUpdates);
    }

    [Fact]
    public void Corrupt_settings_are_reported_once() // 006 corrupt file
    {
        var (_, shell, _) = Create(settingsReset: true);

        Assert.True(shell.IsNotificationOpen);
        Assert.Equal("Settings were reset", shell.NotificationTitle);
    }
}
