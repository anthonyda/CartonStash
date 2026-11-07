using System.Text.RegularExpressions;

namespace CartonStash.Core.Models;

/// <summary>A release on GitHub, read from a package's release-notes URL (spec 012 R2.1).</summary>
public sealed partial record GitHubRelease(string Owner, string Repo, string Tag)
{
    /// <summary>The one API request the app makes for it (R2.3): only owner, repository and tag are sent.</summary>
    public Uri ApiUri => new($"https://api.github.com/repos/{Owner}/{Repo}/releases/tags/{Uri.EscapeDataString(Tag)}");

    /// <summary>
    /// Accepts exactly https://github.com/{owner}/{repo}/releases/tag/{tag}, with no port or user info; the query
    /// and fragment are ignored, and the tag may hold "/". Anything else is null, and the pane keeps only the link.
    /// </summary>
    public static GitHubRelease? TryParse(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase)
            || !uri.IsDefaultPort
            || uri.UserInfo.Length > 0)
        {
            return null;
        }

        // "/owner/repo/releases/tag/v1.2" → ["", owner, repo, "releases", "tag", "v1.2"]; a trailing "/" is ignored.
        var parts = uri.AbsolutePath.TrimEnd('/').Split('/');
        if (parts.Length < 6
            || parts[0].Length != 0
            || !string.Equals(parts[3], "releases", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(parts[4], "tag", StringComparison.OrdinalIgnoreCase)
            || !OwnerPattern().IsMatch(parts[1])
            || !RepoPattern().IsMatch(parts[2])
            || parts[2] is "." or "..")
        {
            return null;
        }

        var tagParts = parts[5..];
        if (Array.Exists(tagParts, part => part.Length == 0))
        {
            return null;
        }
        var tag = Uri.UnescapeDataString(string.Join('/', tagParts));
        return tag.Trim().Length == 0 ? null : new GitHubRelease(parts[1], parts[2], tag);
    }

    [GeneratedRegex("^[A-Za-z0-9-]{1,39}$")]
    private static partial Regex OwnerPattern();

    [GeneratedRegex("^[A-Za-z0-9._-]{1,100}$")]
    private static partial Regex RepoPattern();
}
