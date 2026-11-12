using System.Collections.Concurrent;

namespace CartonStash.Core.Services;

/// <summary>
/// The available version of each package from the latest lists, so icons can be read later without searching again
/// (005 R5). Installed and search entries are kept apart (BR-05): a reload of the installed list drops its own entries
/// only, and the rows of the current search keep theirs until the next search.
/// </summary>
/// <typeparam name="TVersion">WinGet's version object in the app; anything in tests.</typeparam>
internal sealed class VersionCache<TVersion> where TVersion : class
{
    private readonly ConcurrentDictionary<string, TVersion> _installed = new(StringComparer.OrdinalIgnoreCase);
    private volatile ConcurrentDictionary<string, TVersion> _search = new(StringComparer.OrdinalIgnoreCase);

    public void SetInstalled(string id, TVersion version) => _installed[id] = version;

    /// <summary>A new search replaces the last one's entries: its rows are gone.</summary>
    public void SetSearch(IEnumerable<KeyValuePair<string, TVersion>> results) =>
        _search = new ConcurrentDictionary<string, TVersion>(results.DistinctBy(r => r.Key, StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);

    /// <summary>The installed list's own entry, for WinGet's comparison with the installed version (002 R7.1).</summary>
    public TVersion? Installed(string id) => _installed.GetValueOrDefault(id);

    /// <summary>Any entry, for the icon.</summary>
    public TVersion? Any(string id) => _installed.GetValueOrDefault(id) ?? _search.GetValueOrDefault(id);

    /// <summary>After a change to the system: the next installed load fills it again; search entries stay.</summary>
    public void ClearInstalled() => _installed.Clear();
}
