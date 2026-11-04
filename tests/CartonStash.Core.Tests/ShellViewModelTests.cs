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
        var updates = new UpdatesViewModel(app.Store, app.Actions, app.Packages, app.Settings);
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
        var updates = new UpdatesViewModel(app.Store, app.Actions, app.Packages, app.Settings);
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
    public async Task Restart_that_fails_says_how_to_finish() // 004 R6.3
    {
        var (_, shell, _) = CreateWithRestarter(started: false);

        await shell.RestartCommand.ExecuteAsync(null);

        Assert.Equal(NotificationSeverity.Error, shell.NotificationSeverity);
        Assert.Contains("open it again", shell.NotificationMessage);
        Assert.False(shell.HasNotificationRestart);
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
