using CartonStash.Core.Models;
using CartonStash.Core.Services;

namespace CartonStash.Core.Tests;

/// <summary>014 T19: going back asks for the scope the package is installed with (R3.8).</summary>
public class GoingBackScopeTests
{
    private const string Pandoc312 = "{671ADF5A-6B4A-4D75-9EBB-FC09E0A0382F}";

    private static readonly PackageInfo Pandoc = new("JohnMacFarlane.Pandoc", "Pandoc", "3.13", "3.13", "winget") { InstalledId = @"ARP\Machine\X64\{34C3EAA3}" };

    [Theory] // not going back: always the setting, whatever is installed
    [InlineData(InstallScope.Any)]
    [InlineData(InstallScope.User)]
    [InlineData(InstallScope.System)]
    public void ScopeFor_not_going_back_uses_the_setting_R3_8(InstallScope setting)
    {
        Assert.Equal((setting, false), WinGetPackageInstaller.ScopeFor(null, goingBack: false, InstallScope.User, "Machine", setting));
    }

    [Theory] // T18: Windows Installer said UserUnmanaged where WinGet said System, so Windows Installer wins
    [InlineData(InstallScope.User, "Machine", InstallScope.User)]
    [InlineData(InstallScope.System, "User", InstallScope.System)]
    [InlineData(InstallScope.User, null, InstallScope.User)]
    public void ScopeFor_an_MSI_context_wins_over_WinGets_scope_R3_8(InstallScope msi, string? winGet, InstallScope expected)
    {
        Assert.Equal((expected, true), WinGetPackageInstaller.ScopeFor(null, goingBack: true, msi, winGet, InstallScope.Any));
    }

    [Theory] // no MSI answer: WinGet's InstalledScope, by its strings
    [InlineData("User", InstallScope.User)]
    [InlineData("user", InstallScope.User)]
    [InlineData("Machine", InstallScope.System)]
    public void ScopeFor_without_an_MSI_context_uses_WinGets_scope_R3_8(string winGet, InstallScope expected)
    {
        Assert.Equal((expected, true), WinGetPackageInstaller.ScopeFor(null, goingBack: true, null, winGet, InstallScope.Any));
        Assert.Equal((expected, true), WinGetPackageInstaller.ScopeFor(null, goingBack: true, InstallScope.Any, winGet, InstallScope.Any));
    }

    [Theory] // neither knows: the setting (006 R2.1)
    [InlineData("Unknown")]
    [InlineData("")]
    [InlineData(null)]
    public void ScopeFor_with_no_known_scope_uses_the_setting_R3_8(string? winGet)
    {
        Assert.Equal((InstallScope.User, false), WinGetPackageInstaller.ScopeFor(null, goingBack: true, null, winGet, InstallScope.User));
        Assert.Equal((InstallScope.Any, false), WinGetPackageInstaller.ScopeFor(null, goingBack: true, null, winGet, InstallScope.Any));
    }

    [Theory] // the scope carried across the R3.5 uninstall comes before anything else
    [InlineData(InstallScope.User)]
    [InlineData(InstallScope.System)]
    public void ScopeFor_an_explicit_scope_comes_first_R3_8(InstallScope explicitScope)
    {
        Assert.Equal((explicitScope, true), WinGetPackageInstaller.ScopeFor(explicitScope, goingBack: false, null, null, InstallScope.Any));
        var other = explicitScope == InstallScope.User ? InstallScope.System : InstallScope.User;
        Assert.Equal((explicitScope, true), WinGetPackageInstaller.ScopeFor(explicitScope, goingBack: true, other, "Machine", other));
    }

    [Fact]
    public void ScopeFor_an_explicit_Any_is_no_scope_R3_8()
    {
        Assert.Equal((InstallScope.User, false), WinGetPackageInstaller.ScopeFor(InstallScope.Any, goingBack: false, null, null, InstallScope.User));
    }

    [Fact] // machine first, then the per-user contexts (T18: 1605 for the contexts the product isn't in)
    public void The_MSI_context_is_the_first_one_Windows_Installer_has_R3_8()
    {
        Assert.Equal(InstallScope.System, MsiInstallContext.FirstContext(Pandoc312, (_, c) => c == MsiInstallContext.Machine));
        Assert.Equal(InstallScope.User, MsiInstallContext.FirstContext(Pandoc312, (_, c) => c == MsiInstallContext.UserUnmanaged));
        Assert.Equal(InstallScope.User, MsiInstallContext.FirstContext(Pandoc312, (_, c) => c == MsiInstallContext.UserManaged));
        Assert.Equal(InstallScope.System, MsiInstallContext.FirstContext(Pandoc312, (_, _) => true));
        Assert.Null(MsiInstallContext.FirstContext(Pandoc312, (_, _) => false));
    }

    [Theory] // only MSI product codes are asked; WinGet lists other "product codes" for other installer types
    [InlineData("Git_is1")]
    [InlineData("Notepad++")]
    [InlineData("{not-a-guid-at-all-but-thirty-eight-ch}")]
    [InlineData("")]
    public void Codes_that_arent_MSI_product_codes_arent_asked_R3_8(string code)
    {
        var asked = false;
        Assert.Null(MsiInstallContext.FirstContext(code, (_, _) => asked = true));
        Assert.False(asked);
    }

    [Fact] // a failed read is never an error of its own: the next source is asked
    public void A_failing_MSI_read_is_unknown_R3_8()
    {
        Assert.Null(MsiInstallContext.FirstContext(Pandoc312, (_, _) => throw new DllNotFoundException()));
    }

    [Fact] // the real msi.dll: a product that isn't installed is in no context
    public void The_real_MSI_context_of_a_missing_product_is_unknown_R3_8()
    {
        Assert.Null(new MsiInstallContext().ContextOf("{00000000-0000-0000-0000-000000000001}"));
    }

    // --- The scope across the R3.5 uninstall (Uninstall, then install) and R3.7 (Install {previous version})

    private static async Task<(TestApp App, PackageOperation Failed)> RefusedGoingBack(InstallScope scope, bool fromInstalled)
    {
        var app = new TestApp();
        app.Dialogs.OptionAnswer = false;
        await app.Actions.InstallVersionAsync(Pandoc, new("3.12", VersionDirection.Older));
        var failed = app.Queue.Find(Pandoc)!;
        app.Installer.Complete(Pandoc.Id, new OperationOutcome(OperationResult.Failed, "Pandoc couldn't go back to 3.12.")
        {
            OffersUninstallFirst = true,
            Scope = scope,
            ScopeFromInstalled = fromInstalled,
        });
        return (app, failed);
    }

    [Fact]
    public async Task The_operation_keeps_the_scope_its_run_asked_for_R3_8()
    {
        var (_, failed) = await RefusedGoingBack(InstallScope.System, fromInstalled: true);

        Assert.Equal(InstallScope.System, failed.Scope);
        Assert.True(failed.ScopeFromInstalled);
    }

    [Fact]
    public async Task The_install_after_the_uninstall_keeps_the_installed_scope_R3_8()
    {
        var (app, failed) = await RefusedGoingBack(InstallScope.User, fromInstalled: true);

        failed.UninstallThenInstallCommand.Execute(null);
        Assert.Null(app.Installer.Scopes[^1]); // the uninstall itself asks for no scope
        app.Installer.Complete(Pandoc.Id);

        Assert.Equal(OperationKind.Install, app.Installer.Calls[^1].Kind);
        Assert.Equal(InstallScope.User, app.Installer.Scopes[^1]);
    }

    [Fact]
    public async Task Install_previous_keeps_the_installed_scope_too_R3_8()
    {
        var (app, failed) = await RefusedGoingBack(InstallScope.System, fromInstalled: true);
        failed.UninstallThenInstallCommand.Execute(null);
        app.Installer.Complete(Pandoc.Id);
        app.Installer.Complete(Pandoc.Id, OperationResult.Failed, "no");

        app.Queue.Find(Pandoc)!.InstallPreviousCommand.Execute(null);

        Assert.Equal("3.13", app.Installer.Versions[^1]);
        Assert.Equal(InstallScope.System, app.Installer.Scopes[^1]);
    }

    [Theory] // a scope that came from the setting isn't carried: the install after the uninstall follows the setting
    [InlineData(InstallScope.User)]
    [InlineData(InstallScope.Any)]
    public async Task A_scope_from_the_setting_isnt_carried_R3_8(InstallScope scope)
    {
        var (app, failed) = await RefusedGoingBack(scope, fromInstalled: false);

        failed.UninstallThenInstallCommand.Execute(null);
        app.Installer.Complete(Pandoc.Id);

        Assert.Null(app.Installer.Scopes[^1]);
    }

    [Fact] // the first run and ordinary operations pass no explicit scope
    public async Task Going_back_itself_passes_no_explicit_scope_R3_8()
    {
        var app = new TestApp();
        await app.Actions.InstallVersionAsync(Pandoc, new("3.12", VersionDirection.Older));
        await app.Actions.UpdateAsync(Pkg.Installed("B"));

        Assert.All(app.Installer.Scopes, Assert.Null);
    }

    // --- The log

    [Theory]
    [InlineData(InstallScope.System, true, "Scope: for all users, as installed")]
    [InlineData(InstallScope.User, true, "Scope: for the current user, as installed")]
    [InlineData(InstallScope.System, false, "Scope: from Settings (all users)")]
    [InlineData(InstallScope.User, false, "Scope: from Settings (current user)")]
    [InlineData(InstallScope.Any, false, "Scope: from Settings (let WinGet decide)")]
    public void The_log_says_which_scope_was_asked_for_R3_8(InstallScope scope, bool fromInstalled, string line)
    {
        var queue = new PackageOperationQueue(new ScriptedInstaller
        {
            AutoComplete = new OperationOutcome(OperationResult.Succeeded, "ok") { Scope = scope, ScopeFromInstalled = fromInstalled },
        });

        var operation = queue.Enqueue(OperationKind.Install, Pandoc, options: new() { Version = "3.12", GoingBack = true });

        Assert.Contains(line, OperationLogStore.Compose(operation, null));
    }

    [Fact]
    public void An_uninstall_logs_no_scope_R3_8()
    {
        var queue = new PackageOperationQueue(new ScriptedInstaller { AutoComplete = new OperationOutcome(OperationResult.Succeeded, "ok") });

        var operation = queue.Enqueue(OperationKind.Uninstall, Pandoc);

        Assert.Contains("Scope: none (uninstall)", OperationLogStore.Compose(operation, null));
    }
}
