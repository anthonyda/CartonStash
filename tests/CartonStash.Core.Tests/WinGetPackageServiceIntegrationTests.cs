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
