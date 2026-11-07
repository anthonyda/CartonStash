using CartonStash.Core.Models;
using CartonStash.Core.Services;
using static CartonStash.Core.Services.WinGetPackageInstaller;

namespace CartonStash.Core.Tests;

public class AfterUpdateTests
{
    private static readonly OperationOutcome Ok = new(OperationResult.Succeeded, "Updated successfully.", "WinGet status: Ok");

    [Fact] // 013 R2.2: Autodesk Fusion, 2026-11-04 to 07
    public void Same_version_with_an_update_is_a_warning()
    {
        var outcome = AfterUpdate(Ok, "2706.1.14", new InstalledAfterUpdate("2706.1.14", UpdateAvailable: true), "Autodesk Fusion");

        Assert.Equal(OperationResult.SucceededUnconfirmed, outcome.Result);
        Assert.Equal("WinGet finished without an error, but Windows still lists Autodesk Fusion 2706.1.14. "
            + "The installer may still be finishing, or Autodesk Fusion may update itself the next time it starts.", outcome.Message);
        Assert.DoesNotContain("successfully", outcome.Message);
        Assert.Equal("WinGet status: Ok", outcome.Details); // R4.1
    }

    [Theory] // 013 R2.3: the app doesn't guess
    [InlineData("2706.1.14", "2706.1.17", false, true)]  // updated
    [InlineData("2706.1.14", "2706.1.17", true, true)]   // changed, and a newer one exists
    [InlineData("2706.1.14", "2706.1.14", false, true)]  // WinGet no longer offers one
    [InlineData("2706.1.14", null, false, false)]        // couldn't read
    [InlineData("2706.1.14", null, true, true)]          // no longer listed
    [InlineData(null, "2706.1.14", true, true)]          // no version before
    public void Anything_else_stays_a_success(string? before, string? versionAfter, bool updateAvailable, bool readable)
    {
        var after = readable ? new InstalledAfterUpdate(versionAfter, updateAvailable) : null;

        Assert.Same(Ok, AfterUpdate(Ok, before, after, "Autodesk Fusion"));
    }

    [Fact] // 013 R2.1, R2.4: App Installer keeps its own check; other updates get the version check
    public void Successful_updates_are_checked_and_App_Installer_its_own_way()
    {
        Assert.Equal(CheckAfterOperation.InstalledVersion, CheckAfter(OperationKind.Upgrade, Ok, Pkg.Installed("Autodesk.Fusion", update: true)));
        Assert.Equal(CheckAfterOperation.AppInstaller, CheckAfter(OperationKind.Upgrade, Ok, WinGetHostTests.AppInstaller()));
    }

    [Theory] // 013 R2.5: not for installs, uninstalls or failed updates
    [InlineData(OperationKind.Install, OperationResult.Succeeded)]
    [InlineData(OperationKind.Uninstall, OperationResult.Succeeded)]
    [InlineData(OperationKind.Upgrade, OperationResult.Failed)]
    [InlineData(OperationKind.Upgrade, OperationResult.SucceededRestartRequired)]
    public void Other_operations_are_not_checked(OperationKind kind, OperationResult result)
    {
        Assert.Equal(CheckAfterOperation.None, CheckAfter(kind, new OperationOutcome(result, "x"), Pkg.Installed("Autodesk.Fusion", update: true)));
    }
}
