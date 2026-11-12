using CartonStash.Core.Services;

namespace CartonStash.Core.Tests;

/// <summary>BR-05: search rows keep their icons after a reload of the installed list (005 R5.1, R5.2).</summary>
public class VersionCacheTests
{
    [Fact]
    public void A_reload_of_the_installed_list_keeps_the_search_entries_005_R5_2()
    {
        var cache = new VersionCache<string>();
        cache.SetSearch([new("Spotify.Spotify", "search")]);
        cache.SetInstalled("Git.Git", "installed");

        cache.ClearInstalled();

        Assert.Equal("search", cache.Any("Spotify.Spotify"));
        Assert.Null(cache.Any("Git.Git"));
    }

    [Fact]
    public void A_new_search_replaces_the_last_one()
    {
        var cache = new VersionCache<string>();
        cache.SetSearch([new("A", "a")]);

        cache.SetSearch([new("B", "b"), new("b", "duplicate")]);

        Assert.Null(cache.Any("A"));
        Assert.Equal("b", cache.Any("B"));
    }

    [Fact] // the comparison with the installed version only reads the installed list's own entries (002 R7.1)
    public void The_installed_comparison_never_reads_a_search_entry_002_R7_1()
    {
        var cache = new VersionCache<string>();
        cache.SetSearch([new("X", "from another source")]);

        Assert.Null(cache.Installed("X"));
        cache.SetInstalled("X", "installed");
        Assert.Equal("installed", cache.Installed("X"));
        Assert.Equal("installed", cache.Any("X"));
    }
}

/// <summary>BR-29: the same ID from two sources keeps two entries.</summary>
public class CacheKeyTests
{
    [Fact]
    public void Two_sources_with_one_ID_have_two_keys()
    {
        var cache = new VersionCache<string>();
        cache.SetSearch([new(WinGetPackageService.CacheKey("winget", "VideoLAN.VLC"), "public"), new(WinGetPackageService.CacheKey("corp", "VideoLAN.VLC"), "mirror")]);

        Assert.Equal("public", cache.Any(WinGetPackageService.CacheKey("winget", "VideoLAN.VLC")));
        Assert.Equal("mirror", cache.Any(WinGetPackageService.CacheKey("CORP", "videolan.vlc"))); // case-insensitive, as before
    }
}
