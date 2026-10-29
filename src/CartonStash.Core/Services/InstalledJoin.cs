using CartonStash.Core.Models;

namespace CartonStash.Core.Services;

/// <summary>One program from WinGet's installed-only catalog (002 R6.1).</summary>
/// <param name="LocalId">Installed entry ID, e.g. <c>ARP\Machine\X64\{GUID}</c> or <c>MSIX\&lt;full name&gt;</c>.</param>
/// <param name="Codes">Product codes and package family names of the installed program.</param>
internal sealed record InstalledEntry(string LocalId, string Name, string Version, IReadOnlyList<string> Codes);

/// <summary>A package from the local-first composite catalog: WinGet's own match of one installed program to a source.</summary>
/// <param name="InstalledLocalId">The installed entry WinGet evaluated (<c>InstalledVersion.Id</c>).</param>
/// <param name="InstalledVersion">WinGet's view of that entry's version, as <c>winget list</c> shows it (e.g. <c>&gt; 1.8.10</c>).</param>
/// <param name="ManifestCodes">Product codes and package family names listed in the source manifest.</param>
internal sealed record MatchedPackage(
    string Id,
    string Name,
    string? InstalledLocalId,
    string? InstalledVersion,
    string? AvailableVersion,
    string Source,
    bool UpdateAvailable,
    IReadOnlyList<string> ManifestCodes);

/// <summary>
/// Builds the Installed rows (002 amendment R6): one row per installed program, linked to WinGet where WinGet linked it
/// (primary match) or where the program's codes appear in a source manifest (manifest match). Codes, not names: WinGet
/// decides, we don't guess (constitution P1).
/// </summary>
internal static class InstalledJoin
{
    /// <param name="compare">WinGet's comparison of a package's available version with an installed version (002 R7.1).</param>
    public static IReadOnlyList<PackageInfo> Join(IReadOnlyList<InstalledEntry> entries, IReadOnlyList<MatchedPackage> packages,
        Func<MatchedPackage, string, VersionStatus>? compare = null)
    {
        compare ??= (_, _) => VersionStatus.Unknown;
        var entriesById = new Dictionary<string, InstalledEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            entriesById.TryAdd(entry.LocalId, entry);
        }

        var rows = new List<PackageInfo>(entries.Count);
        var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Primary matches: the program WinGet itself evaluated for each package (R6.3, R6.4).
        foreach (var package in packages)
        {
            if (package.InstalledLocalId is { } localId && entriesById.TryGetValue(localId, out var entry) && claimed.Add(localId))
            {
                rows.Add(Row(entry, package, primary: true, compare));
            }
            else if (package.InstalledLocalId is not null && !claimed.Contains(package.InstalledLocalId))
            {
                // Installed entry missing from the installed-only catalog. Not seen in practice (T9); keep WinGet's row.
                rows.Add(new PackageInfo(package.Id, package.Name, package.InstalledVersion ?? "Unknown", package.AvailableVersion, package.Source, package.UpdateAvailable)
                {
                    InstalledId = package.InstalledLocalId,
                });
            }
        }

        // Manifest codes → package, for programs WinGet merged away (e.g. a second WSL).
        var byCode = new Dictionary<string, MatchedPackage>(StringComparer.Ordinal);
        foreach (var package in packages.Where(p => p.Source.Length > 0).OrderBy(p => p.Id, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var code in package.ManifestCodes)
            {
                byCode.TryAdd(Normalize(code), package);
            }
        }

        foreach (var entry in entries)
        {
            if (claimed.Contains(entry.LocalId))
            {
                continue;
            }

            var match = entry.Codes.Select(c => byCode.GetValueOrDefault(Normalize(c))).FirstOrDefault(p => p is not null);
            rows.Add(match is not null
                ? Row(entry, match, primary: false, compare)
                : new PackageInfo(entry.LocalId, entry.Name, entry.Version, null, "") { InstalledId = entry.LocalId });
        }

        return rows;
    }

    private static PackageInfo Row(InstalledEntry entry, MatchedPackage package, bool primary, Func<MatchedPackage, string, VersionStatus> compare)
    {
        // WinGet's own match shows WinGet's version (R6.2); other rows keep what Windows records.
        var version = primary ? package.InstalledVersion ?? entry.Version : entry.Version;
        return new(package.Id, entry.Name, version, package.AvailableVersion, package.Source,
            // Only WinGet's own evaluation may claim an update (R6.4).
            UpdateAvailable: primary && package.UpdateAvailable)
        {
            InstalledId = entry.LocalId,
            IsPrimaryMatch = primary,
            // Only WinGet's own match is compared: a manifest match's recorded version may use another numbering (R7.2).
            VersionStatus = primary && package.AvailableVersion is not null ? compare(package, version) : VersionStatus.Unknown,
        };
    }

    /// <summary>Product codes come with or without braces and in any case.</summary>
    internal static string Normalize(string code) => code.Trim().Trim('{', '}').ToLowerInvariant();
}
