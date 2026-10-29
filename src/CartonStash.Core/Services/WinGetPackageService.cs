using System.Collections.Concurrent;
using Microsoft.Management.Deployment;
using CartonStash.Core.Models;

namespace CartonStash.Core.Services;

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
    // 005 R5: version info of packages from the latest lists, so icons can be read later without searching again.
    // COM objects stay in this class (A2).
    private readonly ConcurrentDictionary<string, PackageVersionInfo> _versions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string?> _iconUrls = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _iconReads = new(4, 4);

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

    /// <summary>
    /// One row per installed program (002 R6): WinGet's installed-only catalog joined with its source matches.
    /// Both reads run in parallel off the UI thread.
    /// </summary>
    public async Task<IReadOnlyList<PackageInfo>> GetInstalledAsync(CancellationToken cancellationToken = default)
    {
        var entries = Task.Run(() => ReadInstalledEntriesAsync(cancellationToken), cancellationToken);
        var packages = Task.Run(() => ReadMatchedPackagesAsync(cancellationToken), cancellationToken);
        await Task.WhenAll(entries, packages);
        // The comparer makes cross-process calls: keep the join off the UI thread.
        return await Task.Run(() => InstalledJoin.Join(entries.Result, packages.Result, CompareWithAvailable), cancellationToken);
    }

    private async Task<IReadOnlyList<InstalledEntry>> ReadInstalledEntriesAsync(CancellationToken cancellationToken)
    {
        var catalog = await connection.GetInstalledOnlyAsync(cancellationToken);
        var matches = await FindMatchesAsync(catalog, new FindPackagesOptions(), cancellationToken);
        var entries = new List<InstalledEntry>(matches.Count);
        for (var i = 0; i < matches.Count; i++)
        {
            var package = matches[i].CatalogPackage;
            var installed = package.InstalledVersion;
            if (installed is null)
            {
                continue;
            }
            entries.Add(new InstalledEntry(package.Id, package.Name, installed.Version, ReadCodes(installed)));
        }
        return entries;
    }

    private async Task<IReadOnlyList<MatchedPackage>> ReadMatchedPackagesAsync(CancellationToken cancellationToken)
    {
        var catalog = await connection.GetLocalFirstAsync(cancellationToken);
        var matches = await FindMatchesAsync(catalog, new FindPackagesOptions(), cancellationToken);
        var packages = new List<MatchedPackage>(matches.Count);
        for (var i = 0; i < matches.Count; i++)
        {
            var package = matches[i].CatalogPackage;
            var available = package.DefaultInstallVersion;
            if (available is not null)
            {
                _versions[package.Id] = available; // 005 R5 icons
            }
            packages.Add(new MatchedPackage(
                package.Id,
                package.Name,
                package.InstalledVersion?.Id,
                package.InstalledVersion?.Version,
                available?.Version,
                available?.PackageCatalog.Info.Name ?? "",
                package.IsUpdateAvailable,
                available is null ? [] : ReadCodes(available)));
        }
        return packages;
    }

    /// <summary>WinGet decides how an installed version compares with a package's available version (002 R7.1, P1).</summary>
    private VersionStatus CompareWithAvailable(MatchedPackage package, string installedVersion) =>
        _versions.TryGetValue(package.Id, out var available) ? Compare(available, installedVersion) : VersionStatus.Unknown;

    internal static VersionStatus ToVersionStatus(CompareResult availableComparedToInstalled) => availableComparedToInstalled switch
    {
        CompareResult.Equal => VersionStatus.Same,
        CompareResult.Greater => VersionStatus.Older, // the source is ahead: installed is older
        CompareResult.Lesser => VersionStatus.Newer, // installed is ahead of the source
        _ => VersionStatus.Unknown,
    };

    private static VersionStatus Compare(PackageVersionInfo available, string? installedVersion)
    {
        if (string.IsNullOrEmpty(installedVersion))
        {
            return VersionStatus.Unknown;
        }
        try
        {
            return ToVersionStatus(available.CompareToVersion(installedVersion));
        }
        catch (Exception ex) when (ex is ArgumentException or System.Runtime.InteropServices.COMException)
        {
            return VersionStatus.Unknown;
        }
    }

    /// <summary>Product codes and package family names, read by index (W2).</summary>
    private static List<string> ReadCodes(PackageVersionInfo version)
    {
        var productCodes = version.ProductCodes;
        var familyNames = version.PackageFamilyNames;
        var codes = new List<string>(productCodes.Count + familyNames.Count);
        for (var i = 0; i < productCodes.Count; i++)
        {
            codes.Add(productCodes[i]);
        }
        for (var i = 0; i < familyNames.Count; i++)
        {
            codes.Add(familyNames[i]);
        }
        return codes;
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

    public async Task<string?> GetIconUrlAsync(PackageInfo package, CancellationToken cancellationToken = default)
    {
        if (_iconUrls.TryGetValue(package.Id, out var cached))
        {
            return cached;
        }
        if (!_versions.TryGetValue(package.Id, out var version))
        {
            return null;
        }

        await _iconReads.WaitAsync(cancellationToken);
        try
        {
            var url = await Task.Run(() => PickIcon(version.GetCatalogPackageMetadata().Icons), cancellationToken);
            _iconUrls[package.Id] = url;
            return url;
        }
        finally
        {
            _iconReads.Release();
        }
    }

    public void InvalidateCache()
    {
        _versions.Clear(); // the next load fills it again; icon URLs stay cached
        connection.Invalidate();
    }

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

    private static async Task<IReadOnlyList<MatchResult>> FindMatchesAsync(PackageCatalog catalog, FindPackagesOptions options, CancellationToken cancellationToken)
    {
        var result = await catalog.FindPackagesAsync(options).AsTask(cancellationToken);
        if (result.Status != FindPackagesResultStatus.Ok)
        {
            throw new WinGetException(DescribeFindFailure(result.Status), result.ExtendedErrorCode);
        }
        return result.Matches;
    }

    private async Task<IReadOnlyList<PackageInfo>> FindAsync(PackageCatalog catalog, FindPackagesOptions options, CancellationToken cancellationToken)
    {
        var matches = await FindMatchesAsync(catalog, options, cancellationToken);
        var packages = new List<PackageInfo>(matches.Count);
        for (var i = 0; i < matches.Count; i++)
        {
            var package = matches[i].CatalogPackage;
            var info = ToPackageInfo(package);
            if (package.DefaultInstallVersion is { } version)
            {
                _versions[info.Id] = version;
            }
            packages.Add(info);
        }
        return packages;
    }

    /// <summary>Prefers the theme-neutral icon; only web URLs (005 R5.2).</summary>
    private static string? PickIcon(IReadOnlyList<Icon> icons)
    {
        string? fallback = null;
        for (var i = 0; i < icons.Count; i++)
        {
            var url = icons[i].Url;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            {
                continue;
            }
            if (icons[i].Theme == IconTheme.Default)
            {
                return url;
            }
            fallback ??= url;
        }
        return fallback;
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
            UpdateAvailable: package.IsUpdateAvailable)
        {
            VersionStatus = available is null ? VersionStatus.Unknown : Compare(available, installed?.Version), // 002 R7.1
        };
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
