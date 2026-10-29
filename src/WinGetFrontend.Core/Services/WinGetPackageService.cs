using Microsoft.Management.Deployment;
using WinGetFrontend.Core.Models;

namespace WinGetFrontend.Core.Services;

/// <summary>
/// <see cref="IPackageService"/> backed by the official WinGet COM API
/// (Microsoft.Management.Deployment). The bundled factory DLL activates the
/// WinGet out-of-process server, so this works from an unpackaged app.
/// </summary>
/// <remarks>
/// Cross-process rules (see specs/constitution.md):
/// the host .exe must sit next to Microsoft.Management.Deployment.winmd, and WinGet collections
/// must be read by index — <c>foreach</c>/LINQ needs IIterable, which the server doesn't marshal.
/// </remarks>
public sealed class WinGetPackageService(CatalogConnection connection) : IPackageService
{
    public WinGetPackageService()
        : this(new CatalogConnection())
    {
    }

    public async Task<IReadOnlyList<PackageInfo>> SearchAsync(string query, int maxResults = 50, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var catalog = await connection.GetRemoteFirstAsync(cancellationToken);

        var options = new FindPackagesOptions { ResultLimit = (uint)maxResults };
        options.Selectors.Add(new PackageMatchFilter
        {
            Field = PackageMatchField.CatalogDefault,
            Option = PackageFieldMatchOption.ContainsCaseInsensitive,
            Value = query.Trim(),
        });

        return await FindAsync(catalog, options, cancellationToken);
    }

    public async Task<IReadOnlyList<PackageInfo>> GetInstalledAsync(CancellationToken cancellationToken = default)
    {
        var catalog = await connection.GetLocalFirstAsync(cancellationToken);

        // No selectors: every installed package (002 R1.1).
        return await FindAsync(catalog, new FindPackagesOptions(), cancellationToken);
    }

    public Task<PackageDetails?> GetDetailsAsync(PackageInfo package, CancellationToken cancellationToken = default)
    {
        if (!package.IsManaged)
        {
            return Task.FromResult<PackageDetails?>(null); // 005 R1.5
        }

        // Metadata calls are synchronous cross-process calls; keep them off the UI thread.
        return Task.Run(async () =>
        {
            var found = await connection.FindByIdAsync(package.Id, package.Source, package.IsInstalled, cancellationToken);
            var version = found.DefaultInstallVersion;
            if (version is null)
            {
                return null;
            }
            cancellationToken.ThrowIfCancellationRequested();
            return ToDetails(version.GetCatalogPackageMetadata(), ReadVersions(found, version));
        }, cancellationToken);
    }

    public void InvalidateCache() => connection.Invalidate();

    public Task<IReadOnlyList<SourceInfo>> GetSourcesAsync(CancellationToken cancellationToken = default) =>
        Task.Run<IReadOnlyList<SourceInfo>>(() =>
        {
            var catalogs = connection.Manager.GetPackageCatalogs();
            var sources = new List<SourceInfo>(catalogs.Count);
            for (var i = 0; i < catalogs.Count; i++)
            {
                var reference = catalogs[i];
                var info = reference.Info;
                var agreements = reference.SourceAgreements;
                var list = new List<Agreement>(agreements.Count);
                for (var j = 0; j < agreements.Count; j++)
                {
                    list.Add(new Agreement(agreements[j].Label, NullIfEmpty(agreements[j].Text), NullIfEmpty(agreements[j].Url)));
                }
                sources.Add(new SourceInfo(
                    info.Name,
                    info.Type,
                    info.Argument,
                    info.TrustLevel == PackageCatalogTrustLevel.Trusted,
                    info.Explicit,
                    info.LastUpdateTime.Year > 1601 ? info.LastUpdateTime : null,
                    list));
            }
            return sources;
        }, cancellationToken);

    public async Task<string?> RefreshSourceAsync(string sourceName, CancellationToken cancellationToken = default)
    {
        var reference = connection.Manager.GetPackageCatalogByName(sourceName);
        if (reference is null)
        {
            return $"Source '{sourceName}' no longer exists.";
        }

        var result = await reference.RefreshPackageCatalogAsync().AsTask(cancellationToken);
        connection.Invalidate(); // 006 R3.2: next search uses fresh data
        return result.Status switch
        {
            RefreshPackageCatalogStatus.Ok => null,
            RefreshPackageCatalogStatus.GroupPolicyError => "Blocked by a group policy on this PC.",
            RefreshPackageCatalogStatus.CatalogError => "Couldn't reach the source. Check your internet connection.",
            _ => $"Refresh failed (WinGet reported {result.Status}).",
        };
    }

    public void AcceptSourceAgreements(string sourceName) => connection.AcceptSourceAgreements(sourceName);

    public string GetWinGetVersion() => connection.Manager.Version;

    private static async Task<IReadOnlyList<PackageInfo>> FindAsync(PackageCatalog catalog, FindPackagesOptions options, CancellationToken cancellationToken)
    {
        var result = await catalog.FindPackagesAsync(options).AsTask(cancellationToken);
        if (result.Status != FindPackagesResultStatus.Ok)
        {
            throw new WinGetException(DescribeFindFailure(result.Status), result.ExtendedErrorCode);
        }

        var matches = result.Matches;
        var packages = new List<PackageInfo>(matches.Count);
        for (var i = 0; i < matches.Count; i++)
        {
            packages.Add(ToPackageInfo(matches[i].CatalogPackage));
        }
        return packages;
    }

    // User-facing messages (spec 001 R3.4). The HRESULT stays on WinGetException.HResult.
    internal static string DescribeConnectFailure(ConnectResultStatus status) => status switch
    {
        ConnectResultStatus.CatalogError =>
            "Couldn't reach the package sources. Check your internet connection and try again.",
        ConnectResultStatus.SourceAgreementsNotAccepted =>
            "A package source requires you to accept its agreement. Open Settings → Sources to review and accept it, then try again.",
        _ => $"Couldn't connect to the package sources (WinGet reported {status}).",
    };

    internal static string DescribeFindFailure(FindPackagesResultStatus status) => status switch
    {
        FindPackagesResultStatus.CatalogError =>
            "Couldn't reach the package sources. Check your internet connection and try again.",
        FindPackagesResultStatus.BlockedByPolicy =>
            "Searching is blocked by a group policy on this PC.",
        _ => $"The search failed (WinGet reported {status}).",
    };

    private static PackageInfo ToPackageInfo(CatalogPackage package)
    {
        var available = package.DefaultInstallVersion;
        var installed = package.InstalledVersion;

        return new PackageInfo(
            Id: package.Id,
            Name: package.Name,
            InstalledVersion: installed?.Version,
            AvailableVersion: available?.Version,
            // No remote version = WinGet can't manage it (002 R3.1).
            Source: available?.PackageCatalog.Info.Name ?? "",
            UpdateAvailable: package.IsUpdateAvailable);
    }

    /// <summary>
    /// Available versions newest first (005 R2.1). WinGet lists them in its own order; we only check the
    /// direction with WinGet's comparer (P1) and never sort version strings ourselves.
    /// </summary>
    private static List<string> ReadVersions(CatalogPackage package, PackageVersionInfo reference)
    {
        var ids = package.AvailableVersions;
        var versions = new List<string>(ids.Count);
        for (var i = 0; i < ids.Count; i++)
        {
            versions.Add(ids[i].Version);
        }
        if (versions.Count > 1 && reference.CompareToVersion(versions[0]) == CompareResult.Greater)
        {
            versions.Reverse();
        }
        return versions;
    }

    private static PackageDetails ToDetails(CatalogPackageMetadata metadata, IReadOnlyList<string> versions)
    {
        var tags = metadata.Tags;
        var tagList = new List<string>(tags.Count);
        for (var i = 0; i < tags.Count; i++)
        {
            tagList.Add(tags[i]);
        }

        return new PackageDetails
        {
            Publisher = NullIfEmpty(metadata.Publisher),
            ShortDescription = NullIfEmpty(metadata.ShortDescription),
            Description = NullIfEmpty(metadata.Description),
            License = NullIfEmpty(metadata.License),
            LicenseUrl = NullIfEmpty(metadata.LicenseUrl),
            Tags = tagList,
            HomepageUrl = NullIfEmpty(metadata.PackageUrl),
            PublisherUrl = NullIfEmpty(metadata.PublisherUrl),
            SupportUrl = NullIfEmpty(metadata.PublisherSupportUrl),
            PrivacyUrl = NullIfEmpty(metadata.PrivacyUrl),
            ReleaseNotes = NullIfEmpty(metadata.ReleaseNotes),
            ReleaseNotesUrl = NullIfEmpty(metadata.ReleaseNotesUrl),
            Versions = versions,
        };
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>A WinGet operation failed; <see cref="Exception.HResult"/> holds WinGet's extended error code.</summary>
public sealed class WinGetException : Exception
{
    public WinGetException(string message, Exception? extendedError)
        : base(message, extendedError)
    {
        if (extendedError is not null)
        {
            HResult = extendedError.HResult;
        }
    }
}
