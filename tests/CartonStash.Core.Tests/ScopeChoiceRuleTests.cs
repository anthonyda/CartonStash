using CartonStash.Core.Services;
using Microsoft.Management.Deployment;

namespace CartonStash.Core.Tests;

/// <summary>015 T5: when the pane offers both scopes, with WinGet's answers measured in T1 as fakes (R1.2).</summary>
public class ScopeChoiceRuleTests
{
    private static Func<PackageInstallScope, PackageInstallerScope?> Answers(PackageInstallerScope? user, PackageInstallerScope? system) =>
        scope => scope == PackageInstallScope.User ? user : scope == PackageInstallScope.System ? system : null;

    [Fact] // Pandoc, Git, VS Code: a per-user and an all-users installer
    public void Two_real_scopes_are_offered_R1_2()
    {
        Assert.True(WinGetPackageService.OffersScopeChoice(Answers(PackageInstallerScope.User, PackageInstallerScope.System)));
    }

    [Theory]
    [InlineData(null, PackageInstallerScope.System)] // 7-Zip: no per-user installer
    [InlineData(PackageInstallerScope.Unknown, PackageInstallerScope.Unknown)] // jq (portable), PowerShell (MSIX)
    [InlineData(PackageInstallerScope.Unknown, PackageInstallerScope.System)] // Notepad++: "User" is a portable zip
    [InlineData(PackageInstallerScope.User, null)]
    [InlineData(null, null)]
    public void Anything_else_is_one_scope_R1_2(PackageInstallerScope? user, PackageInstallerScope? system)
    {
        Assert.False(WinGetPackageService.OffersScopeChoice(Answers(user, system)));
    }
}
