using CartonStash.Core.Models;
using CartonStash.Core.ViewModels;

namespace CartonStash.Core.Tests;

/// <summary>Spec 003 confirmations and agreements; row states.</summary>
public class PackageActionsTests
{
    [Fact]
    public async Task Install_without_agreements_queues_directly() // R1.2
    {
        var app = new TestApp();

        await app.Actions.InstallAsync(Pkg.Remote("A"));

        Assert.Empty(app.Dialogs.AgreementPrompts);
        Assert.Equal([(OperationKind.Install, "A", false)], app.Installer.Calls);
    }

    [Fact]
    public async Task Install_with_agreements_asks_and_installs_only_if_accepted() // R1.3
    {
        var app = new TestApp();
        app.Installer.Agreements = [new Agreement("License", "Do not resell.", null)];

        app.Dialogs.Answer = false;
        await app.Actions.InstallAsync(Pkg.Remote("A", "Contoso"));
        Assert.Empty(app.Installer.Calls);

        app.Dialogs.Answer = true;
        await app.Actions.InstallAsync(Pkg.Remote("A", "Contoso"));

        Assert.Equal(2, app.Dialogs.AgreementPrompts.Count);
        Assert.Equal("Do not resell.", app.Dialogs.AgreementPrompts[0].Agreements[0].Text);
        Assert.Equal([(OperationKind.Install, "A", true)], app.Installer.Calls);
    }

    [Fact]
    public async Task Uninstall_asks_for_confirmation_naming_the_package() // R2.2
    {
        var app = new TestApp();
        var git = Pkg.Installed("Git.Git", "Git");

        app.Dialogs.Answer = false;
        await app.Actions.UninstallAsync(git);
        Assert.Empty(app.Installer.Calls);

        app.Dialogs.Answer = true;
        await app.Actions.UninstallAsync(git);

        Assert.All(app.Dialogs.Confirmations, c => Assert.Contains("Git", c));
        Assert.Equal([(OperationKind.Uninstall, "Git.Git", false)], app.Installer.Calls);
    }

    [Fact]
    public async Task Update_all_confirms_with_the_count() // 004 R2.3
    {
        var app = new TestApp();

        app.Dialogs.Answer = false;
        await app.Actions.UpdateManyAsync([Pkg.Installed("A", update: true), Pkg.Installed("B", update: true)], confirm: true);
        Assert.Empty(app.Installer.Calls);

        app.Dialogs.Answer = true;
        await app.Actions.UpdateManyAsync([Pkg.Installed("A", update: true), Pkg.Installed("B", update: true)], confirm: true);

        Assert.Contains("2 packages will be updated", app.Dialogs.Confirmations[1]);
        Assert.Equal(["A"], app.Installer.Calls.Select(c => c.Id)); // B waits its turn
        Assert.Equal(2, app.Queue.ActiveCount);
    }

    [Fact]
    public async Task Row_shows_progress_instead_of_buttons_while_busy() // R3.1, R4.2
    {
        var app = new TestApp();
        var row = new PackageRowViewModel(Pkg.Remote("A"), app.Actions, RowActions.Install);
        app.Queue.Enqueued += (_, o) => row.Operation = o;
        Assert.True(row.ShowInstall);

        await row.InstallCommand.ExecuteAsync(null);

        Assert.True(row.IsBusy);
        Assert.False(row.ShowInstall);

        app.Installer.Complete("A");

        Assert.False(row.IsBusy);
        Assert.False(row.ShowInstall); // installed now
        Assert.Equal("Installed", row.ResultText);
    }

    [Fact]
    public void New_row_picks_up_an_operation_already_running_for_its_package() // R4.2
    {
        var app = new TestApp();
        app.Queue.Enqueue(OperationKind.Install, Pkg.Remote("A"));

        var row = new PackageRowViewModel(Pkg.Remote("A"), app.Actions, RowActions.Install);

        Assert.True(row.IsBusy);
    }

    [Fact]
    public async Task Failed_install_offers_install_again()
    {
        var app = new TestApp();
        var row = new PackageRowViewModel(Pkg.Remote("A"), app.Actions, RowActions.Install);
        app.Queue.Enqueued += (_, o) => row.Operation = o;

        await row.InstallCommand.ExecuteAsync(null);
        app.Installer.Complete("A", OperationResult.Failed, "nope");

        Assert.True(row.ShowInstall);
        Assert.Equal("Failed", row.ResultText);
    }
}
