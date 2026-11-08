using CartonStash.Core.Models;
using CartonStash.Core.Services;

namespace CartonStash.Core.Tests;

/// <summary>014 T7: when going back fails, Uninstall, then install (R3.4 to R3.7).</summary>
public class UninstallThenInstallTests
{
    private static readonly PackageInfo Git = new("Git.Git", "Git", "2.56.0.4", "2.56.0.4", "winget") { InstalledId = @"ARP\Machine\X64\Git_is1" };

    private static readonly OperationOutcome Refused = new(OperationResult.Failed, "Git couldn't go back to 2.56.0.3.") { OffersUninstallFirst = true };

    /// <summary>Going back to 2.56.0.3, refused by the installer.</summary>
    private static async Task<(TestApp App, PackageOperation Failed)> RefusedGoingBack(bool exclude = true)
    {
        var app = new TestApp();
        app.Dialogs.OptionAnswer = exclude;
        await app.Actions.InstallVersionAsync(Git, new("2.56.0.3", VersionDirection.Older));
        var failed = app.Queue.Find(Git)!;
        app.Installer.Complete(Git.Id, Refused);
        app.Dialogs.Confirmations.Clear();
        return (app, failed);
    }

    [Fact] // R3.5
    public async Task A_refused_going_back_offers_it()
    {
        var (_, failed) = await RefusedGoingBack();

        Assert.True(failed.CanUninstallThenInstall);
        Assert.Equal("Uninstall, then install 2.56.0.3", failed.UninstallThenInstallText);
        Assert.True(failed.CanRetry);
    }

    [Fact] // R3.5: not for 003 R7's running program, nor for other operations
    public void It_is_offered_only_after_the_installer_refused_going_back()
    {
        var app = new TestApp();
        var goingBack = new OperationOptions { Version = "2.56.0.3", GoingBack = true, PreviousVersion = "2.56.0.4" };

        var inUse = app.Queue.Enqueue(OperationKind.Install, Git, options: goingBack);
        app.Installer.Complete(Git.Id, OperationResult.Failed, "Git is running");
        var ordinary = app.Queue.Enqueue(OperationKind.Install, Pkg.Remote("B"), options: new() { Version = "1.0" });
        app.Installer.Complete("B", Refused);

        Assert.False(inUse.CanUninstallThenInstall);
        Assert.False(ordinary.CanUninstallThenInstall);
    }

    [Fact] // R3.5
    public async Task It_asks_first_naming_both_versions()
    {
        var (app, failed) = await RefusedGoingBack();
        app.Dialogs.Answer = false;

        failed.UninstallThenInstallCommand.Execute(null);

        Assert.Equal("Uninstall Git, then install 2.56.0.3? | Git 2.56.0.4 will be uninstalled, then 2.56.0.3 installed. "
            + "Settings and data that Git keeps may be lost. Apps that come as an MSIX package always lose them.", Assert.Single(app.Dialogs.Confirmations));
        Assert.Single(app.Installer.Calls); // R3.4: Cancel queues nothing
    }

    [Fact] // R3.5: uninstall, then install, in that order, with the same acceptance and version
    public async Task Confirming_uninstalls_then_installs_the_chosen_version()
    {
        var (app, failed) = await RefusedGoingBack();

        failed.UninstallThenInstallCommand.Execute(null);
        Assert.Equal((OperationKind.Uninstall, "Git.Git", false), app.Installer.Calls[^1]);
        Assert.Equal(2, app.Installer.Calls.Count); // no install yet
        app.Installer.Complete(Git.Id);

        Assert.Equal(OperationKind.Install, app.Installer.Calls[^1].Kind);
        Assert.Equal("2.56.0.3", app.Installer.Versions[^1]);
        var install = app.Queue.Find(Git)!;
        Assert.True(install.Options.AfterUninstall);
        Assert.False(install.Package.IsInstalled); // an ordinary install now, not going back
    }

    [Theory] // R3.6
    [InlineData(OperationResult.Failed)]
    [InlineData(OperationResult.Cancelled)]
    public async Task A_failed_uninstall_installs_nothing(OperationResult result)
    {
        var (app, failed) = await RefusedGoingBack();

        failed.UninstallThenInstallCommand.Execute(null);
        app.Installer.Complete(Git.Id, result, "The uninstaller failed with error code 5.");

        Assert.Equal(OperationKind.Uninstall, app.Installer.Calls[^1].Kind);
        Assert.Equal("The uninstaller failed with error code 5.", app.Queue.Find(Git)!.Message);
    }

    [Fact] // R3.7: Windows Terminal in T1
    public async Task A_failed_install_after_the_uninstall_says_the_package_is_gone()
    {
        var (app, failed) = await RefusedGoingBack();
        failed.UninstallThenInstallCommand.Execute(null);
        app.Installer.Complete(Git.Id);

        app.Installer.Complete(Git.Id, OperationResult.Failed, "The installer failed with error code 0x80070002.");

        var install = app.Queue.Find(Git)!;
        Assert.Equal("Git is no longer installed: it was uninstalled to make way for 2.56.0.3, and then that install didn't finish. "
            + "The installer failed with error code 0x80070002.", install.Message);
        Assert.True(install.CanInstallPrevious);
        Assert.Equal("Install 2.56.0.4", install.InstallPreviousText);
        Assert.False(install.CanUninstallThenInstall);
    }

    [Fact] // R3.7
    public async Task Install_previous_installs_the_version_from_before_with_no_flags()
    {
        var (app, failed) = await RefusedGoingBack();
        failed.UninstallThenInstallCommand.Execute(null);
        app.Installer.Complete(Git.Id);
        app.Installer.Complete(Git.Id, OperationResult.Failed, "no");

        app.Queue.Find(Git)!.InstallPreviousCommand.Execute(null);

        Assert.Equal(OperationKind.Install, app.Installer.Calls[^1].Kind);
        Assert.Equal("2.56.0.4", app.Installer.Versions[^1]);
        var previous = app.Queue.Find(Git)!;
        Assert.Equal(new OperationOptions { Version = "2.56.0.4" }, previous.Options);
        app.Installer.Complete(Git.Id);
        Assert.False(app.Settings.Current.IsExcluded(Git.Id)); // R3.7: no exclusion
    }

    [Fact] // R3.5: the R3.2 choice carries over, and applies only once the install worked
    public async Task The_exclusion_follows_the_chained_install()
    {
        var (app, failed) = await RefusedGoingBack(exclude: true);

        failed.UninstallThenInstallCommand.Execute(null);
        app.Installer.Complete(Git.Id); // uninstall
        Assert.False(app.Settings.Current.IsExcluded(Git.Id));
        app.Installer.Complete(Git.Id); // install

        Assert.True(app.Settings.Current.IsExcluded(Git.Id));
    }

    [Fact] // R3.7 wording only for the chained install
    public void Other_failures_keep_their_message()
    {
        var app = new TestApp();
        var op = app.Queue.Enqueue(OperationKind.Install, Pkg.Remote("A"), options: new() { Version = "1.0" });

        app.Installer.Complete("A", OperationResult.Failed, "no");

        Assert.Equal("no", op.Message);
        Assert.False(op.CanInstallPrevious);
    }
}
