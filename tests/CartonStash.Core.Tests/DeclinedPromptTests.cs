using Microsoft.Management.Deployment;
using CartonStash.Core.Models;
using CartonStash.Core.Services;
using static CartonStash.Core.Services.WinGetPackageInstaller;

namespace CartonStash.Core.Tests;

/// <summary>018 amendment R2: a declined permission prompt is Cancelled, not Failed. Codes measured in the VM (T9).</summary>
public class DeclinedPromptTests
{
    // Progress reports run inline, as on the UI thread; without this they go to the thread pool and race the asserts.
    public DeclinedPromptTests() => SynchronizationContext.SetSynchronizationContext(new InlineSynchronizationContext());
    private static readonly PackageInfo Seven = new("7zip.7zip", "7-Zip", "26.05", "26.06", "winget", UpdateAvailable: true);
    private const int BlockedByPolicy = unchecked((int)0x8A15010F); // WinGet's reading of 1625

    private sealed class OpenProbe : IPermissionPromptProbe
    {
        public bool Open { get; set; } = true;

        public bool IsOpen() => Open;
    }

    [Fact] // the maintainer's No on VLC's prompt (T9): exit code 1602, WinGet's "cancelled by user"
    public void A_declined_prompt_is_marked_and_keeps_its_message_R2_2_R2_3()
    {
        var outcome = MapInstallResult(OperationKind.Upgrade, InstallResultStatus.InstallError, 1602, false, InstallScope.Any, InstallCancelledByUser, "VLC");

        Assert.Equal(OperationResult.Failed, outcome.Result);
        Assert.True(outcome.MayBeDeclinedPrompt);
        Assert.Equal("The installer failed with error code 1602.", outcome.Message);
    }

    [Fact] // 1625 came from ending consent.exe, not from a user's No: it means a policy, and stays a failure
    public void Exit_code_1625_is_not_marked_R2_2()
    {
        var outcome = MapInstallResult(OperationKind.Upgrade, InstallResultStatus.InstallError, 1625, false, InstallScope.Any, BlockedByPolicy, "7-Zip");

        Assert.False(outcome.MayBeDeclinedPrompt);
    }

    [Fact]
    public void An_uninstall_declined_the_same_way_is_marked_R2_2()
    {
        var outcome = MapUninstallResult(UninstallResultStatus.UninstallError, 1602, false, InstallCancelledByUser, "7-Zip");

        Assert.True(outcome.MayBeDeclinedPrompt);
    }

    [Fact]
    public void WinGets_cancelled_by_user_alone_is_marked_R2_2()
    {
        var outcome = MapInstallResult(OperationKind.Install, InstallResultStatus.InstallError, 1, false, InstallScope.Any, InstallCancelledByUser, "VLC");

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
        Assert.Contains("Installer exit code: 1602", operation.Details); // R2.4: the code stays in the log
    }

    [Fact]
    public void Without_the_prompt_seen_it_stays_a_failure_R2_3()
    {
        var installer = new ScriptedInstaller();
        var queue = new PackageOperationQueue(installer);
        _ = new PermissionPromptMonitor(queue, new OpenProbe { Open = false });
        var operation = queue.Enqueue(OperationKind.Upgrade, Seven);

        installer.Complete(Seven.Id, Declined());

        Assert.Equal(OperationResult.Failed, operation.Result);
        Assert.Equal("The installer failed with error code 1602.", operation.Message);
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
        MapInstallResult(OperationKind.Upgrade, InstallResultStatus.InstallError, 1602, false, InstallScope.Any, InstallCancelledByUser, "VLC")
            with { Details = FormatDetails("InstallError", 1602, null) };
}
