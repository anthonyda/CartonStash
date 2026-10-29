namespace WinGetFrontend.Core.Models;

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

    /// <summary>Available versions, newest first (005 R2.1).</summary>
    public IReadOnlyList<string> Versions { get; init; } = [];
}

/// <summary>A license or terms text the user must accept (003 R1.3, 006 R3.3).</summary>
public sealed record Agreement(string Label, string? Text, string? Url);
