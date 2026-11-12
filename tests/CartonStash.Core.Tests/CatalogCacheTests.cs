using CartonStash.Core.Services;

namespace CartonStash.Core.Tests;

/// <summary>BR-15: a connection that began before an invalidate isn't kept.</summary>
public class CatalogCacheTests
{
    [Fact]
    public void A_connection_from_before_an_invalidate_is_used_once_but_not_kept()
    {
        var cache = new CatalogCache<string>();
        var started = cache.Begin(); // the details pane connects while an install runs
        cache.Invalidate();          // the install ends

        Assert.Equal("before the install", cache.Keep(started, "before the install"));
        Assert.Null(cache.Value);    // the reload connects again

        var again = cache.Begin();
        cache.Keep(again, "after the install");
        Assert.Equal("after the install", cache.Value);
    }
}
