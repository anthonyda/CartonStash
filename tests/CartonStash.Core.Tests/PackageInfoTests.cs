using CartonStash.Core.Models;

namespace CartonStash.Core.Tests;

public class PackageInfoTests
{
    [Fact]
    public void Not_installed_when_no_installed_version() // R2.1
    {
        var p = new PackageInfo("Git.Git", "Git", null, "2.56", "winget", UpdateAvailable: true);

        Assert.False(p.IsInstalled);
        Assert.False(p.HasUpdate);
    }

    [Fact]
    public void Installed_without_update() // R2.1
    {
        var p = new PackageInfo("Git.Git", "Git", "2.56", "2.56", "winget");

        Assert.True(p.IsInstalled);
        Assert.False(p.HasUpdate);
    }

    [Fact]
    public void Installed_with_update_reported_by_winget() // R2.2
    {
        var p = new PackageInfo("Git.Git", "Git", "2.9", "2.10", "winget", UpdateAvailable: true);

        Assert.True(p.HasUpdate);
    }

    [Fact]
    public void Unknown_installed_version_follows_winget_verdict() // R2.2
    {
        var noUpdate = new PackageInfo("X.Y", "X", "Unknown", "1.0", "winget", UpdateAvailable: false);

        Assert.True(noUpdate.IsInstalled);
        Assert.False(noUpdate.HasUpdate);
    }
}
