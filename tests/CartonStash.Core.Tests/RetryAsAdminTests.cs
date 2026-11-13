using CartonStash.Core.Models;
using CartonStash.Core.Services;
using Microsoft.Management.Deployment;

namespace CartonStash.Core.Tests;

/// <summary>Spec 017: Retry as administrator.</summary>
public class RetryAsAdminTests
{
    [Theory] // R1.1, R1.2
    [InlineData(0x88A30033u, 0)]
    [InlineData(5u, 0)]
    [InlineData(0x80070005u, 0)]
    [InlineData(1925u, 0)]
    [InlineData(0x80073D28u, 0)]
    public void A_lack_of_administrator_rights_says_so_and_offers_the_retry_R1_1(uint installerCode, int extendedError)
    {
        var outcome = WinGetPackageInstaller.MapInstallResult(OperationKind.Upgrade, InstallResultStatus.InstallError, installerCode, false,
            InstallScope.Any, extendedError, "Microsoft 365 Copilot");

        Assert.Equal(OperationResult.Failed, outcome.Result);
        Assert.Equal("Microsoft 365 Copilot's installer needs administrator rights and didn't ask for them.", outcome.Message);
        Assert.True(outcome.OffersRetryAsAdmin);
    }

    [Fact] // R1.2: nothing else is guessed
    public void Other_installer_codes_dont_offer_it_R1_2()
    {
        var outcome = WinGetPackageInstaller.MapInstallResult(OperationKind.Upgrade, InstallResultStatus.InstallError, 1603, false, InstallScope.Any);

        Assert.False(outcome.OffersRetryAsAdmin);
    }

    private static readonly PackageInfo Copilot = Pkg.Installed("Microsoft.365Copilot", "Microsoft 365 Copilot", update: true);

    private static OperationOutcome LacksRights => new(OperationResult.Failed, "needs administrator rights") { OffersRetryAsAdmin = true };

    [Fact] // R1.3, R1.4, R1.6: the same operation again, through the elevated runner and the queue
    public async Task Retry_as_administrator_runs_the_same_update_elevated_R1_4()
    {
        var elevated = new FakeElevated();
        var (app, queue) = Create(elevated);
        var first = queue.Enqueue(OperationKind.Upgrade, Copilot, options: new() { Version = "19.2609.33020.0" });
        app.Installer.Complete(Copilot.Id, LacksRights);
        await queue.Idle;
        Assert.True(first.CanRetryAsAdmin);

        first.RetryAsAdminCommand.Execute(null);
        await queue.Idle;

        var request = Assert.Single(elevated.Requests);
        Assert.Equal(OperationKind.Upgrade, request.Kind);
        Assert.Equal(Copilot.Id, request.Package.Id);
        Assert.Equal("19.2609.33020.0", request.Version);
        var second = queue.Operations[0];
        Assert.NotSame(first, second);
        Assert.Equal(OperationResult.Succeeded, second.Result);
        Assert.True(second.MayAskPermission); // R1.5
        Assert.Single(app.Installer.Calls); // the elevated run doesn't go through the normal installer
    }

    [Fact] // R1.5: the prompt declined
    public async Task A_declined_prompt_is_cancelled_R1_5()
    {
        var elevated = new FakeElevated { Outcome = ElevatedProtocol.Declined() };
        var (app, queue) = Create(elevated);
        var first = queue.Enqueue(OperationKind.Upgrade, Copilot);
        app.Installer.Complete(Copilot.Id, LacksRights);
        await queue.Idle;

        first.RetryAsAdminCommand.Execute(null);
        await queue.Idle;

        Assert.Equal(OperationResult.Cancelled, queue.Operations[0].Result);
        Assert.Equal("Windows' permission prompt was declined.", queue.Operations[0].Message);
    }

    [Fact] // never offered again after a run that had the rights
    public async Task An_elevated_run_doesnt_offer_it_again()
    {
        var elevated = new FakeElevated { Outcome = LacksRights };
        var (app, queue) = Create(elevated);
        var first = queue.Enqueue(OperationKind.Upgrade, Copilot);
        app.Installer.Complete(Copilot.Id, LacksRights);
        await queue.Idle;

        first.RetryAsAdminCommand.Execute(null);
        await queue.Idle;

        Assert.False(queue.Operations[0].CanRetryAsAdmin);
    }

    [Fact] // the request travels on the command line; only installs and updates of one package are accepted
    public void The_request_round_trips_and_nothing_else_is_accepted()
    {
        var request = new ElevatedRequest(OperationKind.Upgrade, Copilot with { InstalledId = @"MSIX\x" }, true, "1.0", @"C:\logs\a.log",
            InstallScope.System, ScopeSource.Chosen, InstallScope.User);

        Assert.Equal(request, ElevatedProtocol.Decode(ElevatedProtocol.Encode(request)));
        Assert.Null(ElevatedProtocol.Decode(ElevatedProtocol.Encode(request with { Kind = OperationKind.Uninstall })));
        Assert.Null(ElevatedProtocol.Decode("not base64!"));
        var outcome = LacksRights with { Details = "WinGet status: Ok", Scope = InstallScope.System };
        Assert.Equal(outcome, ElevatedProtocol.ReadOutcome(ElevatedProtocol.WriteOutcome(outcome)));
    }

    private static (TestApp App, PackageOperationQueue Queue) Create(FakeElevated elevated)
    {
        var app = new TestApp();
        var queue = new PackageOperationQueue(app.Installer, elevated: elevated);
        _ = new CartonStash.Core.ViewModels.PackageActions(queue, app.Installer, app.Dialogs, app.Packages, app.Settings); // follow-ups
        return (app, queue);
    }

    private sealed class FakeElevated : IElevatedRunner
    {
        public List<ElevatedRequest> Requests { get; } = [];

        public OperationOutcome Outcome { get; init; } = new(OperationResult.Succeeded, "Updated successfully.");

        public Task<OperationOutcome> RunAsync(ElevatedRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(Outcome);
        }
    }
}
