namespace WinGetFrontend.Core.Models;

/// <summary>
/// A package as the UI sees it. Plain data, decoupled from the WinGet COM types
/// so view models and tests never touch COM objects directly.
/// </summary>
public sealed record PackageInfo(
    string Id,
    string Name,
    string? InstalledVersion,
    string? AvailableVersion,
    string Source)
{
    public bool IsInstalled => InstalledVersion is not null;

    public bool HasUpdate =>
        IsInstalled && AvailableVersion is not null && AvailableVersion != InstalledVersion;
}
