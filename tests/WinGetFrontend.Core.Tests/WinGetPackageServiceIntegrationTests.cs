using WinGetFrontend.Core.Services;

namespace WinGetFrontend.Core.Tests;

/// <summary>
/// Talks to the real WinGet on this machine (needs network for the winget source).
/// Skip with: dotnet test --filter "Category!=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class WinGetPackageServiceIntegrationTests
{
    [Fact]
    public async Task Search_finds_git()
    {
        var service = new WinGetPackageService();

        var results = await service.SearchAsync("Git.Git");

        Assert.Contains(results, p => p.Id == "Git.Git");
    }
}
