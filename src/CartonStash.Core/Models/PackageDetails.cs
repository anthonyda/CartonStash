namespace CartonStash.Core.Models;

/// <summary>Metadata for the details pane (spec 005). Every field is optional: manifests vary a lot.</summary>
public sealed record PackageDetails
{
    public string? Publisher { get; init; }
    public string? ShortDescription { get; init; }
    public string? Description { get; init; }
    public string? License { get; init; }
    public string? LicenseUrl { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public string? HomepageUrl { get; init; }
    public string? PublisherUrl { get; init; }
    public string? SupportUrl { get; init; }
    public string? PrivacyUrl { get; init; }
    public string? ReleaseNotes { get; init; }
    public string? ReleaseNotesUrl { get; init; }

    /// <summary>Available versions, newest first (005 R2.1), each with how it compares with the installed one (014 R1.2).</summary>
    public IReadOnlyList<PackageVersion> Versions { get; init; } = [];

    /// <summary>
    /// 015 R1.1, R1.2: not installed, and WinGet has a per-user and an all-users installer for the default version, so the
    /// pane's Install offers both scopes.
    /// </summary>
    public bool OffersScopeChoice { get; init; }

    /// <summary>015 R2.1: how the installed version is installed (Windows Installer, then WinGet); null when unknown.</summary>
    public InstallScope? InstalledScope { get; init; }
}

/// <summary>How an available version compares with the installed one, as WinGet's comparer decides (014 R1.2).</summary>
public enum VersionDirection
{
    /// <summary>The package isn't installed: "Install".</summary>
    NotInstalled,

    /// <summary>The installed version itself: no action.</summary>
    Installed,

    /// <summary>Newer than the installed one: "Update to this version".</summary>
    Newer,

    /// <summary>Older than the installed one: "Go back to this version".</summary>
    Older,

    /// <summary>WinGet can't compare it with the installed one: "Install this version", confirmed as for Older.</summary>
    Unknown,
}

/// <summary>One available version of a package (014 R1.1).</summary>
public sealed record PackageVersion(string Version, VersionDirection Direction);

/// <summary>A license or terms text the user must accept (003 R1.3, 006 R3.3).</summary>
public sealed record Agreement(string Label, string? Text, string? Url);
