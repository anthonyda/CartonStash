using Microsoft.Management.Deployment;
using CartonStash.Core.Models;
using CartonStash.Core.Services;
using static CartonStash.Core.Services.WinGetPackageInstaller;

namespace CartonStash.Core.Tests;

/// <summary>018 amendment R2: a declined permission prompt is Cancelled, not Failed. Codes measured in the VM (T9).</summary>
public class DeclinedPromptTests
{
    private static readonly PackageInfo Seven = new("7zip.7zip", "7-Zip", "26.05", "26.06", "winget", UpdateAvailable: true);
    private const int BlockedByPolicy = unchecked((int)0x8A15010F); // what WinGet reported with 1625, both times

    private sealed class OpenProbe : IPermissionPromptProbe
    {
        public bool Open { get; set; } = true;

        public bool IsOpen() => Open;
    }

    [Fact]
    public void Exit_code_1625_is_marked_and_keeps_its_message_R2_2_R2_3()
    {
        var outcome = MapInstallResult(OperationKind.Upgrade, InstallResultStatus.InstallError, 1625, false, InstallScope.Any, BlockedByPolicy, "7-Zip");

        Assert.Equal(OperationResult.Failed, outcome.Result);
        Assert.True(outcome.MayBeDeclinedPrompt);
        Assert.Equal("The installer failed with error code 1625.", outcome.Message);
    }

    [Fact]
    public void An_uninstall_declined_the_same_way_is_marked_R2_2()
    {
        var outcome = MapUninstallResult(UninstallResultStatus.UninstallError, 1625, false, BlockedByPolicy, "7-Zip");

        Assert.True(outcome.MayBeDeclinedPrompt);
    }

    [Fact]
    public void WinGets_cancelled_by_user_is_marked_R2_2()
    {
        var outcome = MapInstallResult(OperationKind.Install, InstallResultStatus.InstallError, 1602, false, InstallScope.Any, InstallCancelledByUser, "VLC");

        Assert.True(outcome.MayBeDeclinedPrompt);
    }

    [Theory]
    [InlineData(1603u, 0)]
    [InlineData(5u, 0)] // 017's access denied keeps Retry as administrator
    public void Other_codes_are_not_marked_R2_2(uint exitCode, int extended)
    {
        var outcome = MapInstallResult(OperationKind.Upgrade, InstallResultStatus.InstallError, exitCode, false, InstallScope.Any, extended, "7-Zip");

        Assert.False(outcome.MayBeDeclinedPrompt);
    }

    [Fact]
    public void With_the_prompt_seen_the_result_is_Cancelled_with_Retry_R2_1()
    {
        var installer = new ScriptedInstaller();
        var queue = new PackageOperationQueue(installer);
        var monitor = new PermissionPromptMonitor(queue, new OpenProbe());
        var operation = queue.Enqueue(OperationKind.Upgrade, Seven);
        installer.Report(Seven.Id, OperationPhase.Running);
        monitor.Tick(DateTimeOffset.Now);

        installer.Complete(Seven.Id, Declined());

        Assert.Equal(OperationResult.Cancelled, operation.Result);
        Assert.Equal("Windows' permission prompt was declined.", operation.Message);
        Assert.True(operation.CanRetry);
        Assert.False(operation.CanRetryAsAdmin); // R2.4
        Assert.Contains("Installer exit code: 1625", operation.Details); // R2.4: the code stays in the log
    }

    [Fact]
    public void Without_the_prompt_seen_1625_stays_a_failure_R2_3()
    {
        var installer = new ScriptedInstaller();
        var queue = new PackageOperationQueue(installer);
        _ = new PermissionPromptMonitor(queue, new OpenProbe { Open = false });
        var operation = queue.Enqueue(OperationKind.Upgrade, Seven);

        installer.Complete(Seven.Id, Declined());

        Assert.Equal(OperationResult.Failed, operation.Result);
        Assert.Equal("The installer failed with error code 1625.", operation.Message);
    }

    [Fact]
    public void A_retry_forgets_the_prompt_of_the_run_before_R2_1()
    {
        var installer = new ScriptedInstaller();
        var queue = new PackageOperationQueue(installer);
        var probe = new OpenProbe();
        var monitor = new PermissionPromptMonitor(queue, probe);
        var operation = queue.Enqueue(OperationKind.Upgrade, Seven);
        monitor.Tick(DateTimeOffset.Now);
        installer.Complete(Seven.Id, Declined());

        probe.Open = false;
        queue.Retry(operation);
        installer.Complete(Seven.Id, Declined());

        Assert.Equal(OperationResult.Failed, operation.Result);
    }

    private static OperationOutcome Declined() =>
        MapInstallResult(OperationKind.Upgrade, InstallResultStatus.InstallError, 1625, false, InstallScope.Any, BlockedByPolicy, "7-Zip")
            with { Details = FormatDetails("InstallError", 1625, null) };
}
