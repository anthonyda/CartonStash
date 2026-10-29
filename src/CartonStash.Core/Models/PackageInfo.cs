namespace CartonStash.Core.Models;

/// <summary>
/// How the installed version compares with the source's available version, as WinGet's comparer decides (002 R7.1).
/// </summary>
public enum VersionStatus
{
    Unknown,
    Same,
    Older,
    Newer,
}

/// <summary>
/// A package as the UI sees it. Plain data, decoupled from the WinGet COM types
/// so view models and tests never touch COM objects directly.
/// </summary>
/// <param name="UpdateAvailable">WinGet's own verdict (<c>CatalogPackage.IsUpdateAvailable</c>);
/// we never compare version strings ourselves.</param>
/// <param name="Source">The source the package is matched to; empty when WinGet doesn't manage it (002 R3.1).</param>
public sealed record PackageInfo(
    string Id,
    string Name,
    string? InstalledVersion,
    string? AvailableVersion,
    string Source,
    bool UpdateAvailable = false)
{
    /// <summary>The installed program's own entry ID (<c>ARP\...</c>, <c>MSIX\...</c>); null for search results (002 R6).</summary>
    public string? InstalledId { get; init; }

    /// <summary>
    /// False when the row was linked to <see cref="Id"/> through the source manifest rather than by WinGet itself; such
    /// rows never claim WinGet's update verdict (002 R6.4).
    /// </summary>
    public bool IsPrimaryMatch { get; init; } = true;

    /// <summary>Installed vs available, decided by WinGet (002 R7.1). Unknown for unmanaged or uncomparable versions.</summary>
    public VersionStatus VersionStatus { get; init; }

    /// <summary>Identity of the row: several installed programs can share one WinGet ID (002 R6.5).</summary>
    public string Key => InstalledId ?? Id;

    public bool IsInstalled => InstalledVersion is not null;

    public bool HasUpdate => IsInstalled && UpdateAvailable;

    /// <summary>WinGet can install, upgrade or uninstall it (002 R3.1).</summary>
    public bool IsManaged => Source.Length > 0;

    /// <summary>Text for the source column: the source name, or "Not managed by WinGet" (002 R3.1).</summary>
    public string SourceText => IsManaged ? Source : "Not managed by WinGet";
}
