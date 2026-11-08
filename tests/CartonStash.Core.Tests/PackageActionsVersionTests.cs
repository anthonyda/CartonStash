using CartonStash.Core.Models;
using CartonStash.Core.ViewModels;

namespace CartonStash.Core.Tests;

/// <summary>014 T6: installing a chosen version from the details pane.</summary>
public class PackageActionsVersionTests
{
    private static readonly PackageInfo PowerToys = new("Microsoft.PowerToys", "PowerToys", "0.102.0", "0.102.0", "winget");

    [Fact] // R1.1, R2.1: newer is an update, asked nothing
    public async Task A_newer_version_is_updated_to_without_a_question()
    {
        var app = new TestApp();

        await app.Actions.InstallVersionAsync(PowerToys, new("0.103.0", VersionDirection.Newer));

        Assert.Empty(app.Dialogs.OptionPrompts);
        Assert.Equal([(OperationKind.Upgrade, "Microsoft.PowerToys", false)], app.Installer.Calls);
        Assert.Equal(["0.103.0"], app.Installer.Versions);
    }

    [Fact] // R1.1: not installed is a plain install
    public async Task A_version_of_a_package_not_installed_is_installed()
    {
        var app = new TestApp();

        await app.Actions.InstallVersionAsync(Pkg.Remote("A"), new("0.9", VersionDirection.NotInstalled));

        Assert.Empty(app.Dialogs.OptionPrompts);
        Assert.Equal(OperationKind.Install, app.Installer.Calls.Single().Kind);
        Assert.Equal(["0.9"], app.Installer.Versions);
    }

    [Fact] // R3.1
    public async Task Going_back_asks_first_naming_both_versions()
    {
        var app = new TestApp();

        await app.Actions.InstallVersionAsync(PowerToys, new("0.101.0", VersionDirection.Older));

        var prompt = Assert.Single(app.Dialogs.OptionPrompts);
        Assert.Equal("Go back to PowerToys 0.101.0?", prompt.Title);
        Assert.Equal("PowerToys 0.102.0 is installed. Going back to 0.101.0 runs that version's installer over it. "
            + "Some installers can't go back, and PowerToys's settings may not carry over to an older version.", prompt.Message);
        Assert.Equal("Leave PowerToys out of update checks", prompt.Option);
        Assert.True(prompt.Checked); // R3.2: checked by default
        Assert.Equal(OperationKind.Install, app.Installer.Calls.Single().Kind);
    }

    [Fact] // R1.2: a version WinGet can't compare is confirmed as going back
    public async Task An_uncomparable_version_asks_too()
    {
        var app = new TestApp();

        await app.Actions.InstallVersionAsync(PowerToys, new("0.101.0", VersionDirection.Unknown));

        Assert.Equal("Install PowerToys 0.101.0?", Assert.Single(app.Dialogs.OptionPrompts).Title);
    }

    [Fact] // R3.1
    public async Task Cancel_queues_nothing()
    {
        var app = new TestApp();
        app.Dialogs.Answer = false;

        await app.Actions.InstallVersionAsync(PowerToys, new("0.101.0", VersionDirection.Older));

        Assert.Empty(app.Installer.Calls);
        Assert.Empty(app.Queue.Operations);
    }

    [Theory] // R3.2: only after it worked
    [InlineData(OperationResult.Succeeded, true)]
    [InlineData(OperationResult.SucceededRestartRequired, true)]
    [InlineData(OperationResult.SucceededUnconfirmed, false)]
    [InlineData(OperationResult.Failed, false)]
    [InlineData(OperationResult.Cancelled, false)]
    public async Task The_exclusion_is_added_only_after_success(OperationResult result, bool excluded)
    {
        var app = new TestApp();

        await app.Actions.InstallVersionAsync(PowerToys, new("0.101.0", VersionDirection.Older));
        Assert.False(app.Settings.Current.IsExcluded(PowerToys.Id)); // not before
        app.Installer.Complete(PowerToys.Id, result);

        Assert.Equal(excluded, app.Settings.Current.IsExcluded(PowerToys.Id));
    }

    [Fact] // R3.2
    public async Task An_unchecked_box_adds_no_exclusion()
    {
        var app = new TestApp();
        app.Dialogs.OptionAnswer = false;

        await app.Actions.InstallVersionAsync(PowerToys, new("0.101.0", VersionDirection.Older));
        app.Installer.Complete(PowerToys.Id);

        Assert.False(app.Settings.Current.IsExcluded(PowerToys.Id));
    }

    [Fact] // R3.2: no box for a package already left out
    public async Task An_excluded_package_gets_no_box()
    {
        var app = new TestApp();
        app.Settings.Update(s => s.WithExcluded(PowerToys.Id));

        await app.Actions.InstallVersionAsync(PowerToys, new("0.101.0", VersionDirection.Older));

        Assert.Null(Assert.Single(app.Dialogs.OptionPrompts).Option);
        Assert.Single(app.Installer.Calls);
    }

    [Fact] // R2.2: the chosen version's agreements
    public async Task Agreements_are_read_for_the_chosen_version()
    {
        var app = new TestApp();

        await app.Actions.InstallVersionAsync(PowerToys, new("0.103.0", VersionDirection.Newer));

        Assert.Equal(["0.103.0"], app.Installer.AgreementVersions);
    }

    [Fact] // R2.2
    public async Task Declined_agreements_queue_nothing()
    {
        var app = new TestApp();
        app.Installer.Agreements = [new Agreement("License", "Terms", null)];
        app.Dialogs.Answer = false;

        await app.Actions.InstallVersionAsync(PowerToys, new("0.103.0", VersionDirection.Newer));

        Assert.Empty(app.Installer.Calls);
    }

    [Fact] // 003 R4.2, R1.5
    public async Task Nothing_while_the_package_is_busy_or_for_the_installed_version()
    {
        var app = new TestApp();
        app.Queue.Enqueue(OperationKind.Upgrade, PowerToys);

        await app.Actions.InstallVersionAsync(PowerToys, new("0.101.0", VersionDirection.Older));
        await new TestApp().Actions.InstallVersionAsync(PowerToys, new("0.102.0", VersionDirection.Installed));

        Assert.Single(app.Installer.Calls);
        Assert.Empty(app.Dialogs.OptionPrompts);
    }

    [Fact] // R3.4: going back never uninstalls by itself
    public async Task Going_back_queues_no_uninstall()
    {
        var app = new TestApp();

        await app.Actions.InstallVersionAsync(PowerToys, new("0.101.0", VersionDirection.Older));
        app.Installer.Complete(PowerToys.Id, OperationResult.Failed, "refused");

        Assert.DoesNotContain(app.Installer.Calls, c => c.Kind == OperationKind.Uninstall);
    }
}
