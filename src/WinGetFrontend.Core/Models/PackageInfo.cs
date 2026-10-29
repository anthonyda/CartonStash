namespace WinGetFrontend.Core.Models;

/// <summary>
/// A package as the UI sees it. Plain data, decoupled from the WinGet COM types
/// so view models and tests never touch COM objects directly.
/// </summary>
/// <param name="UpdateAvailable">WinGet's own verdict (<c>CatalogPackage.IsUpdateAvailable</c>);
/// we never compare version strings ourselves.</param>
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
}
