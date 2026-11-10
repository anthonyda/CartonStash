using CartonStash.Core.Models;
using CartonStash.Core.Services;

namespace CartonStash.Core.Tests;

/// <summary>014 T20: going back that leaves a second copy is a warning (R3.9).</summary>
public class AfterGoingBackTests
{
    private static readonly OperationOutcome Ok = new(OperationResult.Succeeded, "Installed successfully.");

    [Fact]
    public void More_entries_after_is_a_warning_R3_9()
    {
        var outcome = WinGetPackageInstaller.AfterGoingBack(Ok, before: 1, after: 2, "Pandoc", "3.12", "3.13");

        Assert.Equal(OperationResult.SucceededUnconfirmed, outcome.Result);
        Assert.Equal("Pandoc 3.12 was installed next to 3.13, not over it. Both are now installed. "
            + "To keep only 3.12, uninstall Pandoc 3.13 from the Installed page.", outcome.Message);
    }

    [Theory] // same, fewer, or a count that couldn't be read: unchanged (013 R2.3: a failed check is no error)
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(2, 2)]
    [InlineData(null, 2)]
    [InlineData(1, null)]
    [InlineData(null, null)]
    public void Otherwise_the_outcome_is_unchanged_R3_9(int? before, int? after)
    {
        Assert.Same(Ok, WinGetPackageInstaller.AfterGoingBack(Ok, before, after, "Pandoc", "3.12", "3.13"));
    }

    [Theory] // only a plain success is checked: a failure or a restart keeps its own result
    [InlineData(OperationResult.Failed)]
    [InlineData(OperationResult.SucceededRestartRequired)]
    public void Only_a_plain_success_becomes_a_warning_R3_9(OperationResult result)
    {
        var outcome = new OperationOutcome(result, "x");

        Assert.Same(outcome, WinGetPackageInstaller.AfterGoingBack(outcome, 1, 2, "Pandoc", "3.12", "3.13"));
    }

    [Fact] // R3.2: the exclusion is added only on a real success, not on this warning
    public async Task No_exclusion_after_the_warning_R3_9()
    {
        var app = new TestApp();
        app.Dialogs.OptionAnswer = true;
        var pandoc = Pkg.Installed("JohnMacFarlane.Pandoc", "Pandoc");

        await app.Actions.InstallVersionAsync(pandoc, new("3.12", VersionDirection.Older));
        app.Installer.Complete(pandoc.Id, WinGetPackageInstaller.AfterGoingBack(Ok, 1, 2, "Pandoc", "3.12", "3.13"));

        Assert.False(app.Settings.Current.IsExcluded(pandoc.Id));
    }
}
