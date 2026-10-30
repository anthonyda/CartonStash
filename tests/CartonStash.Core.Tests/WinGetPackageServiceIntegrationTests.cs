using CartonStash.Core.Models;
using CartonStash.Core.Services;

namespace CartonStash.Core.Tests;

/// <summary>
/// Talks to the real WinGet on this machine (needs network for the winget source).
/// Skip with: dotnet test --filter "Category!=Integration"
/// They never install or remove software (003 design).
/// </summary>
[Trait("Category", "Integration")]
public class WinGetPackageServiceIntegrationTests
{
    private readonly WinGetPackageService _service = new();

    [Fact]
    public async Task Search_finds_git()
    {
        var results = await _service.SearchAsync("Git.Git");

        Assert.Contains(results, p => p.Id == "Git.Git");
    }

    [Fact]
    public async Task GetInstalled_contains_git() // 002 R1.1, R1.2
    {
        var installed = await _service.GetInstalledAsync();

        var git = Assert.Single(installed, p => p.Id == "Git.Git");
        Assert.NotNull(git.InstalledVersion);
        Assert.Equal("winget", git.Source);
        Assert.True(installed.Count > 10);
    }

    [Fact]
    public async Task GetInstalled_has_one_row_per_installed_program() // 002 R6.1, R6.5
    {
        var installed = await _service.GetInstalledAsync();

        Assert.All(installed, p => Assert.NotNull(p.InstalledId));
        Assert.Equal(installed.Count, installed.Select(p => p.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public async Task Programs_sharing_a_package_id_keep_their_own_versions() // 002 R6.2, R6.4 (WSL on the dev machine)
    {
        var installed = await _service.GetInstalledAsync();

        foreach (var group in installed.Where(p => p.IsManaged).GroupBy(p => p.Id).Where(g => g.Count() > 1))
        {
            Assert.Single(group, p => p.IsPrimaryMatch);
            Assert.All(group.Where(p => !p.IsPrimaryMatch), p => Assert.False(p.HasUpdate));
        }

        var wsl = installed.Where(p => p.Id == "Microsoft.WSL").ToList();
        if (wsl.Count > 1)
        {
            Assert.Equal(wsl.Count, wsl.Select(p => p.InstalledVersion).Distinct().Count());
        }
    }

    [Fact]
    public async Task Version_status_comes_from_winget() // 002 R7.1 (dev machine: Git current, Chatterino ahead of winget)
    {
        var installed = await _service.GetInstalledAsync();

        var git = Assert.Single(installed, p => p.Id == "Git.Git");
        if (git.InstalledVersion == git.AvailableVersion)
        {
            Assert.Equal(VersionStatus.Same, git.VersionStatus);
        }
        if (installed.SingleOrDefault(p => p.Id == "ChatterinoTeam.Chatterino") is { InstalledVersion: "2.5.6", AvailableVersion: "2.5.5" } chatterino)
        {
            Assert.Equal(VersionStatus.Newer, chatterino.VersionStatus);
        }
        Assert.All(installed.Where(p => !p.IsManaged), p => Assert.Equal(VersionStatus.Unknown, p.VersionStatus));
    }

    [Fact]
    public async Task Installed_package_is_found_in_its_own_source() // 003 R1.2: update and details look it up this way
    {
        var connection = new CatalogConnection();
        var installed = await new WinGetPackageService(connection).GetInstalledAsync();
        var git = Assert.Single(installed, p => p.Id == "Git.Git");

        var found = await connection.FindByIdAsync(git.Id, git.Source, localFirst: true, CancellationToken.None);

        Assert.Equal(git.Source, found.DefaultInstallVersion.PackageCatalog.Info.Name);
    }

    [Fact]
    public async Task Package_is_not_found_in_a_source_that_does_not_have_it() // 003 R1.2: no fallback to another source
    {
        var connection = new CatalogConnection();

        await Assert.ThrowsAsync<WinGetException>(
            () => connection.FindByIdAsync("Git.Git", "no-such-source", localFirst: false, CancellationToken.None));
    }

    [Fact]
    public async Task Cached_catalog_is_reused_and_invalidate_reconnects() // 002 T1
    {
        var first = await _service.SearchAsync("Git.Git");
        _service.InvalidateCache();
        var second = await _service.SearchAsync("Git.Git");

        Assert.Equal(first.Select(p => p.Id), second.Select(p => p.Id));
    }

    [Fact]
    public async Task Details_for_git_have_publisher_and_homepage() // 005 R1.2, R2.1
    {
        var git = (await _service.SearchAsync("Git.Git")).First(p => p.Id == "Git.Git");

        var details = await _service.GetDetailsAsync(git);

        Assert.NotNull(details);
        Assert.False(string.IsNullOrEmpty(details.Publisher));
        Assert.StartsWith("https://", details.HomepageUrl);
        Assert.True(details.Versions.Count > 1);
        Assert.Equal(git.AvailableVersion, details.Versions[0]); // newest first
    }

    [Fact]
    public async Task Sources_include_winget_and_version_is_known() // 006 R3.1, R4.1
    {
        var sources = await _service.GetSourcesAsync();

        var winget = Assert.Single(sources, s => s.Name == "winget");
        Assert.True(winget.IsTrusted);
        Assert.False(string.IsNullOrEmpty(winget.Argument));
        Assert.Matches(@"^v?\d+\.\d+", _service.GetWinGetVersion());
    }

    [Fact]
    public async Task Refresh_winget_source_succeeds() // 006 R3.2 (slow)
    {
        Assert.Null(await _service.RefreshSourceAsync("winget"));
    }
}
