namespace CartonStash.Core.Models;

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
    public bool IsInstalled => InstalledVersion is not null;

    public bool HasUpdate => IsInstalled && UpdateAvailable;

    /// <summary>WinGet can install, upgrade or uninstall it (002 R3.1).</summary>
    public bool IsManaged => Source.Length > 0;

    /// <summary>Text for the source column: the source name, or "Not managed by WinGet" (002 R3.1).</summary>
    public string SourceText => IsManaged ? Source : "Not managed by WinGet";
}
