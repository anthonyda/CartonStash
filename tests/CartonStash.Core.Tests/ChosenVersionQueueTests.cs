using CartonStash.Core.Models;
using CartonStash.Core.Services;

namespace CartonStash.Core.Tests;

/// <summary>014 T3: a chosen version travels with the operation, to WinGet, through Retry and into the log.</summary>
public class ChosenVersionQueueTests
{
    private static readonly OperationOptions Chosen = new() { Version = "0.101.0" };

    [Fact]
    public void The_installer_receives_the_chosen_version() // R2.1
    {
        var app = new TestApp();

        app.Queue.Enqueue(OperationKind.Install, Pkg.Installed("A"), acceptAgreements: true, Chosen);

        Assert.Equal(["0.101.0"], app.Installer.Versions);
    }

    [Fact]
    public void Retry_installs_the_same_version_with_the_same_acceptance() // R2.3
    {
        var app = new TestApp();
        var op = app.Queue.Enqueue(OperationKind.Install, Pkg.Installed("A"), acceptAgreements: true, Chosen);
        app.Installer.Complete("A", OperationResult.Failed, "no");

        op.RetryCommand.Execute(null);

        Assert.Equal(["0.101.0", "0.101.0"], app.Installer.Versions);
        Assert.All(app.Installer.Calls, c => Assert.True(c.AcceptAgreements));
    }

    [Fact]
    public void Operations_without_a_choice_use_the_default_version() // R2.4
    {
        var app = new TestApp();

        var op = app.Queue.Enqueue(OperationKind.Upgrade, Pkg.Installed("A", update: true));
        app.Queue.EnqueueMany(OperationKind.Upgrade, [Pkg.Installed("B", update: true)]);
        app.Installer.Complete("A");

        Assert.Equal([null, null], app.Installer.Versions);
        Assert.Null(op.Version);
    }

    [Fact]
    public void The_title_names_the_chosen_version() // R2.1
    {
        var app = new TestApp();

        var chosen = app.Queue.Enqueue(OperationKind.Install, Pkg.Installed("A", "PowerToys"), options: Chosen);
        var plain = app.Queue.Enqueue(OperationKind.Install, Pkg.Remote("B", "Notepad++"));

        Assert.Equal("Install PowerToys 0.101.0", chosen.Title);
        Assert.Equal("Install Notepad++", plain.Title);
    }

    [Fact]
    public void The_log_names_the_chosen_version() // R2.1
    {
        var queue = new PackageOperationQueue(new ScriptedInstaller { AutoComplete = new OperationOutcome(OperationResult.Succeeded, "ok") });

        var chosen = queue.Enqueue(OperationKind.Install, Pkg.Installed("A"), options: Chosen);
        var plain = queue.Enqueue(OperationKind.Install, Pkg.Remote("B"));

        Assert.Contains("Chosen version: 0.101.0", OperationLogStore.Compose(chosen, null));
        Assert.Contains("Chosen version: default", OperationLogStore.Compose(plain, null));
    }
}
