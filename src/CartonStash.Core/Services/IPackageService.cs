using CartonStash.Core.Models;

namespace CartonStash.Core.Services;

/// <summary>
/// The app's only entry point into WinGet for reading. View models depend on this interface,
/// so they can be tested against a fake. Changes to the system go through <see cref="IPackageInstaller"/>.
/// </summary>
public interface IPackageService
{
    /// <summary>Searches all configured remote sources by name, ID or moniker.</summary>
    Task<IReadOnlyList<PackageInfo>> SearchAsync(string query, int maxResults = 50, CancellationToken cancellationToken = default);

    /// <summary>Sources left out because they failed on their own, with the reason (011 R2.1). Empty while all work.</summary>
    IReadOnlyList<SourceFailure> SourceFailures { get; }

    /// <summary>Every installed package, correlated with the remote sources (002 R1.1).</summary>
    Task<IReadOnlyList<PackageInfo>> GetInstalledAsync(CancellationToken cancellationToken = default);

    /// <summary>Metadata and versions for the details pane (005). Returns null when WinGet has no metadata (R1.5).</summary>
    Task<PackageDetails?> GetDetailsAsync(PackageInfo package, CancellationToken cancellationToken = default);

    /// <summary>
    /// The package as WinGet lists it now, with how it's installed (014 R2.6, R2.7). Keeps <paramref name="package"/>'s
    /// installed entry ID. Null when it can't be read: the caller keeps what it has (013 R2.3).
    /// </summary>
    Task<PackageInfo?> ReadInstalledAsync(PackageInfo package, CancellationToken cancellationToken = default);

    /// <summary>
    /// The package's icon URL from WinGet, or null (005 R5). Works for packages from the latest search or installed list;
    /// results are cached for the session.
    /// </summary>
    Task<string?> GetIconUrlAsync(PackageInfo package, CancellationToken cancellationToken = default);

    /// <summary>Drops cached catalogs so the next call sees fresh data (002 R1.3).</summary>
    void InvalidateCache();

    /// <summary>The configured sources (006 R3.1).</summary>
    Task<IReadOnlyList<SourceInfo>> GetSourcesAsync(CancellationToken cancellationToken = default);

    /// <summary>Refreshes one source. Returns null on success, or a readable error (006 R3.2).</summary>
    Task<string?> RefreshSourceAsync(string sourceName, CancellationToken cancellationToken = default);

    /// <summary>Sources not in use because their agreements aren't accepted (006 R3.10). Known after the first connection.</summary>
    IReadOnlyList<string> LeftOutSources { get; }

    /// <summary>Reads a source's agreements, which reaches the source. Returns it unchanged if they can't be read (006 R3.8).</summary>
    Task<SourceInfo> ReadSourceAgreementsAsync(SourceInfo source, CancellationToken cancellationToken = default);

    /// <summary>Saves the user's answer for the agreements shown. Withdrawing is declining (006 R3.9, R3.11).</summary>
    void SetSourceAgreementAnswer(SourceInfo source, bool accepted);

    /// <summary>The WinGet (App Installer) version (006 R4.1).</summary>
    string GetWinGetVersion();
}

/// <summary>Runs one install / uninstall / upgrade (spec 003). Used only by <see cref="PackageOperationQueue"/>.</summary>
public interface IPackageInstaller
{
    /// <summary>
    /// Agreements the user must accept before installing (003 R1.3): of <paramref name="version"/>, or of WinGet's default
    /// version when null (014 R2.2). Empty when there are none.
    /// </summary>
    Task<IReadOnlyList<Agreement>> GetAgreementsAsync(PackageInfo package, string? version = null, CancellationToken cancellationToken = default);

    /// <summary>Runs the operation. Throws <see cref="OperationCanceledException"/> when cancelled.</summary>
    /// <param name="version">The version to install or update to; null for WinGet's default (014 R2.1).</param>
    /// <param name="logPath">Where the installer should write its log, or null (009 R2).</param>
    /// <param name="scope">A scope to ask for whatever else applies: the installed one, carried across the uninstall of 014 R3.5. Null otherwise.</param>
    Task<OperationOutcome> RunAsync(OperationKind kind, PackageInfo package, bool acceptAgreements, string? version, string? logPath,
        IProgress<OperationProgress> progress, CancellationToken cancellationToken, InstallScope? scope = null);
}
