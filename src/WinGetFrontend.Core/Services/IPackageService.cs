using WinGetFrontend.Core.Models;

namespace WinGetFrontend.Core.Services;

/// <summary>
/// The app's only entry point into WinGet for reading. View models depend on this interface,
/// so they can be tested against a fake. Changes to the system go through <see cref="IPackageInstaller"/>.
/// </summary>
public interface IPackageService
{
    /// <summary>Searches all configured remote sources by name, ID or moniker.</summary>
    Task<IReadOnlyList<PackageInfo>> SearchAsync(string query, int maxResults = 50, CancellationToken cancellationToken = default);

    /// <summary>Every installed package, correlated with the remote sources (002 R1.1).</summary>
    Task<IReadOnlyList<PackageInfo>> GetInstalledAsync(CancellationToken cancellationToken = default);

    /// <summary>Metadata and versions for the details pane (005). Returns null when WinGet has no metadata (R1.5).</summary>
    Task<PackageDetails?> GetDetailsAsync(PackageInfo package, CancellationToken cancellationToken = default);

    /// <summary>Drops cached catalogs so the next call sees fresh data (002 R1.3).</summary>
    void InvalidateCache();

    /// <summary>The configured sources (006 R3.1).</summary>
    Task<IReadOnlyList<SourceInfo>> GetSourcesAsync(CancellationToken cancellationToken = default);

    /// <summary>Refreshes one source. Returns null on success, or a readable error (006 R3.2).</summary>
    Task<string?> RefreshSourceAsync(string sourceName, CancellationToken cancellationToken = default);

    /// <summary>Accepts a source's agreements for this session (006 R3.3).</summary>
    void AcceptSourceAgreements(string sourceName);

    /// <summary>The WinGet (App Installer) version (006 R4.1).</summary>
    string GetWinGetVersion();
}

/// <summary>Runs one install / uninstall / upgrade (spec 003). Used only by <see cref="PackageOperationQueue"/>.</summary>
public interface IPackageInstaller
{
    /// <summary>Agreements the user must accept before installing (003 R1.3). Empty when there are none.</summary>
    Task<IReadOnlyList<Agreement>> GetAgreementsAsync(PackageInfo package, CancellationToken cancellationToken = default);

    /// <summary>Runs the operation. Throws <see cref="OperationCanceledException"/> when cancelled.</summary>
    Task<OperationOutcome> RunAsync(OperationKind kind, PackageInfo package, bool acceptAgreements,
        IProgress<OperationProgress> progress, CancellationToken cancellationToken);
}
