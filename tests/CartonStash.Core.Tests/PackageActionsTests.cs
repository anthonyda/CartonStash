using CartonStash.Core.Models;
using CartonStash.Core.Services;
using CartonStash.Core.ViewModels;

namespace CartonStash.Core.Tests;

/// <summary>Spec 003 confirmations and agreements; row states.</summary>
public class PackageActionsTests
{
    private static readonly IReadOnlyList<Agreement> License = [new Agreement("Microsoft Software License Terms", null, "https://powerbi.microsoft.com/desktop-eula/")];

    [Fact]
    public async Task Update_with_agreements_asks_and_updates_only_if_accepted() // 004 R7.1
    {
        var app = new TestApp();
        app.Installer.Agreements = License;
        var powerBi = Pkg.Installed("Microsoft.PowerBI", "Microsoft PowerBI Desktop (x64)", update: true);

        app.Dialogs.Answer = false;
        await app.Actions.UpdateAsync(powerBi);
        Assert.Empty(app.Installer.Calls);
        Assert.Empty(app.Queue.Operations);

        app.Dialogs.Answer = true;
        await app.Actions.UpdateAsync(powerBi);

        Assert.Equal("Microsoft PowerBI Desktop (x64): license agreement", app.Dialogs.AgreementPrompts[1].Title);
        Assert.Equal([(OperationKind.Upgrade, "Microsoft.PowerBI", true)], app.Installer.Calls);
    }

    [Fact]
    public async Task Update_without_agreements_asks_nothing() // 004 R7.1
    {
        var app = new TestApp();

        await app.Actions.UpdateAsync(Pkg.Installed("A", update: true));

        Assert.Empty(app.Dialogs.AgreementPrompts);
        Assert.Equal([(OperationKind.Upgrade, "A", false)], app.Installer.Calls);
    }

    [Fact]
    public async Task Update_goes_ahead_when_agreements_cant_be_read() // 004 R7.4
    {
        var app = new TestApp();
        app.Installer.AgreementsFail = true;

        await app.Actions.UpdateAsync(Pkg.Installed("A", update: true));

        Assert.Empty(app.Dialogs.AgreementPrompts);
        Assert.Equal([(OperationKind.Upgrade, "A", false)], app.Installer.Calls);
    }

    [Fact]
    public async Task Update_all_asks_for_agreements_before_queuing_and_leaves_out_declined() // 004 R7.2, R6.5
    {
        var app = new TestApp();
        app.Installer.AutoComplete = new OperationOutcome(OperationResult.Succeeded, "ok");
        app.Installer.AgreementsById["P"] = License;
        app.Installer.AgreementsById["Q"] = License;
        app.Dialogs.AgreementAnswers.Enqueue(true);  // P
        app.Dialogs.AgreementAnswers.Enqueue(false); // Q

        await app.Actions.UpdateManyAsync(
            [WinGetHostTests.AppInstaller(), Pkg.Installed("P", update: true), Pkg.Installed("A", update: true), Pkg.Installed("Q", update: true)],
            confirm: true);

        Assert.Single(app.Dialogs.Confirmations);
        Assert.Equal(["P: license agreement", "Q: license agreement"], app.Dialogs.AgreementPrompts.Select(p => p.Title));
        Assert.Equal(
            [(OperationKind.Upgrade, "P", true), (OperationKind.Upgrade, "A", false), (OperationKind.Upgrade, "Microsoft.AppInstaller", false)],
            app.Installer.Calls);
        Assert.DoesNotContain(app.Queue.Operations, o => o.Package.Id == "Q");
    }

    [Fact]
    public async Task Nothing_runs_until_every_agreement_is_answered() // 004 R7.2
    {
        var app = new TestApp();
        app.Installer.AgreementsById["B"] = License;
        var callsWhenAsked = -1;
        var dialogs = new BlockingAgreementDialogs(() => callsWhenAsked = app.Installer.Calls.Count);
        var actions = new PackageActions(app.Queue, app.Installer, dialogs, app.Packages);

        await actions.UpdateManyAsync([Pkg.Installed("A", update: true), Pkg.Installed("B", update: true)], confirm: false);

        Assert.Equal(0, callsWhenAsked);
        Assert.Equal(["A"], app.Installer.Calls.Select(c => c.Id)); // A runs, B waits its turn
    }

    private sealed class BlockingAgreementDialogs(Action onAsked) : IDialogService
    {
        private readonly FakeDialogs _inner = new();

        public Task<bool> ConfirmAsync(string title, string message, string confirmText, string cancelText = "Cancel") =>
            _inner.ConfirmAsync(title, message, confirmText, cancelText);

        public Task ShowLogAsync(string title, string text, string folder) => _inner.ShowLogAsync(title, text, folder);

        public Task<AgreementAnswer> ShowSourceAgreementAsync(SourceInfo source, string? receivesNotice) =>
            _inner.ShowSourceAgreementAsync(source, receivesNotice);

        public Task<bool> ShowAgreementsAsync(string title, IReadOnlyList<Agreement> agreements)
        {
            onAsked();
            return Task.FromResult(true);
        }
    }

    [Fact]
    public async Task App_Installer_is_queued_last() // 004 R6.5
    {
        var app = new TestApp();
        app.Installer.AutoComplete = new OperationOutcome(OperationResult.Succeeded, "ok");

        await app.Actions.UpdateManyAsync(
            [Pkg.Installed("B", update: true), WinGetHostTests.AppInstaller(), Pkg.Installed("A", update: true), Pkg.Installed("C", update: true)],
            confirm: false);

        Assert.Equal(["B", "A", "C", "Microsoft.AppInstaller"], app.Installer.Calls.Select(c => c.Id));
    }

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
