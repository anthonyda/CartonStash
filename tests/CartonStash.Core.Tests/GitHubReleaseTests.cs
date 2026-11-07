using CartonStash.Core.Models;

namespace CartonStash.Core.Tests;

/// <summary>Spec 012 R2.1, R2.3.</summary>
public class GitHubReleaseTests
{
    [Theory] // The release-notes URLs in the requirements sample (2026-11-06).
    [InlineData("https://github.com/git-for-windows/git/releases/tag/v2.56.0.windows.4", "git-for-windows", "git", "v2.56.0.windows.4")]
    [InlineData("https://github.com/ip7z/7zip/releases/tag/26.05", "ip7z", "7zip", "26.05")]
    [InlineData("https://github.com/microsoft/PowerToys/releases/tag/v0.102.3101", "microsoft", "PowerToys", "v0.102.3101")]
    [InlineData("https://github.com/notepad-plus-plus/notepad-plus-plus/releases/tag/v8.9.8.1", "notepad-plus-plus", "notepad-plus-plus", "v8.9.8.1")]
    [InlineData("https://github.com/JanDeDobbeleer/oh-my-posh/releases/tag/v31.9.1", "JanDeDobbeleer", "oh-my-posh", "v31.9.1")]
    [InlineData("https://github.com/microsoft/terminal/releases/tag/v1.25.2733.0", "microsoft", "terminal", "v1.25.2733.0")]
    [InlineData("https://github.com/BurntSushi/ripgrep/releases/tag/15.2.0", "BurntSushi", "ripgrep", "15.2.0")]
    [InlineData("https://github.com/sharkdp/bat/releases/tag/v0.26.1", "sharkdp", "bat", "v0.26.1")]
    public void Accepts_release_pages(string url, string owner, string repo, string tag)
    {
        Assert.Equal(new GitHubRelease(owner, repo, tag), GitHubRelease.TryParse(url));
    }

    [Theory]
    [InlineData("https://GitHub.com/sharkdp/bat/releases/tag/v0.26.1")]
    [InlineData("https://github.com/sharkdp/bat/releases/tag/v0.26.1/")]
    [InlineData("https://github.com/sharkdp/bat/releases/tag/v0.26.1?x=1#notes")]
    [InlineData("https://github.com:443/sharkdp/bat/releases/tag/v0.26.1")]
    public void Ignores_case_trailing_slash_query_and_fragment(string url)
    {
        Assert.Equal(new GitHubRelease("sharkdp", "bat", "v0.26.1"), GitHubRelease.TryParse(url));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("http://github.com/sharkdp/bat/releases/tag/v0.26.1")]
    [InlineData("https://www.github.com/sharkdp/bat/releases/tag/v0.26.1")]
    [InlineData("https://github.com.evil.example/sharkdp/bat/releases/tag/v0.26.1")]
    [InlineData("https://gitlab.com/sharkdp/bat/releases/tag/v0.26.1")]
    [InlineData("https://github.com:8443/sharkdp/bat/releases/tag/v0.26.1")]
    [InlineData("https://user:pass@github.com/sharkdp/bat/releases/tag/v0.26.1")]
    [InlineData("https://github.com/sharkdp/bat/releases")]
    [InlineData("https://github.com/sharkdp/bat/releases/latest")]
    [InlineData("https://github.com/sharkdp/bat/releases/tag/")]
    [InlineData("https://github.com/sharkdp/bat/releases/tag/%20")]
    [InlineData("https://github.com/sharkdp/bat/blob/master/CHANGELOG.md")]
    [InlineData("https://github.com/sharkdp/bat/tree/v0.26.1")]
    [InlineData("https://github.com/shark_dp/bat/releases/tag/v0.26.1")]
    [InlineData("https://github.com/sharkdp/b%40t/releases/tag/v0.26.1")]
    [InlineData("https://github.com/sharkdp/../releases/tag/v0.26.1")]
    [InlineData("https://github.com/sharkdp/bat/releases/tag/a//b")]
    [InlineData("https://github.com/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/bat/releases/tag/v1")] // 40 characters
    public void Refuses_anything_else(string? url) => Assert.Null(GitHubRelease.TryParse(url));

    [Fact]
    public void A_tag_with_a_slash_is_read_whole_and_sent_as_one_segment() // R2.1, R2.3
    {
        var release = GitHubRelease.TryParse("https://github.com/owner/repo/releases/tag/app/v1.0");
        Assert.Equal(new GitHubRelease("owner", "repo", "app/v1.0"), release);
        Assert.Equal("https://api.github.com/repos/owner/repo/releases/tags/app%2Fv1.0", release!.ApiUri.AbsoluteUri);
    }

    [Fact]
    public void An_escaped_tag_is_unescaped_then_escaped_again() // R2.3
    {
        var release = GitHubRelease.TryParse("https://github.com/owner/repo/releases/tag/v1.0%2Bbuild");
        Assert.Equal("v1.0+build", release!.Tag);
        Assert.Equal("https://api.github.com/repos/owner/repo/releases/tags/v1.0%2Bbuild", release.ApiUri.AbsoluteUri);
    }
}
