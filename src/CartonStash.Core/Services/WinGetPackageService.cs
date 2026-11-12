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
/// <param name="installContext">Windows Installer's context of MSI products (014 R3.8); <see cref="MsiInstallContext"/> when null.</param>
public sealed class WinGetPackageService(CatalogConnection connection, IInstallContext? installContext = null) : IPackageService
{
    private readonly IInstallContext _installContext = installContext ?? new MsiInstallContext();

    public async Task<PackageInfo?> ReadInstalledAsync(PackageInfo package, CancellationToken cancellationToken = default)
    {
        try
        {
            // The installed catalog correlated with the sources, as the Installed page reads it. The queue's operations
            // invalidate the cache when they finish, so this is fresh after one (002 R1.3).
            var found = await connection.FindByIdAsync(package.Id, package.IsManaged ? package.Source : null, localFirst: true, cancellationToken);
            return await Task.Run(() => ToPackageInfo(found) with
            {
                InstalledId = package.InstalledId,
                IsPrimaryMatch = package.IsPrimaryMatch,
                InstalledScope = found.InstalledVersion is null ? null : InstalledScopes.Read(found.InstalledVersion, _installContext),
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // 005 R5: version info of packages from the latest lists, so icons can be read later without searching again.
    // COM objects stay in this class (A2).
    private readonly VersionCache<PackageVersionInfo> _versions = new();
    private readonly ConcurrentDictionary<string, string?> _iconUrls = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _iconReads = new(4, 4);


    public async Task<IReadOnlyList<PackageInfo>> SearchAsync(string query, int maxResults = 50, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        FindPackagesOptions Options()
        {
            var options = new FindPackagesOptions { ResultLimit = (uint)maxResults };
            options.Selectors.Add(new PackageMatchFilter
            {
                Field = PackageMatchField.CatalogDefault,
                Option = PackageFieldMatchOption.ContainsCaseInsensitive,
                Value = query.Trim(),
            });
            return options;
        }

        var matches = await connection.FindAsync(localFirst: false, Options, cancellationToken);
        // BR-16 (A4): about ten calls into WinGet's process per result; off the UI thread, as the installed list.
        return await Task.Run(() => ToPackages(matches), cancellationToken);
    }

    public IReadOnlyList<SourceFailure> SourceFailures => connection.SourceFailures;

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
        var matches = await CatalogConnection.FindMatchesAsync(catalog, new FindPackagesOptions(), cancellationToken);
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
        var matches = await connection.FindAsync(localFirst: true, () => new FindPackagesOptions(), cancellationToken);
        var packages = new List<MatchedPackage>(matches.Count);
        for (var i = 0; i < matches.Count; i++)
        {
            var package = matches[i].CatalogPackage;
            var available = package.DefaultInstallVersion;
            if (available is not null)
            {
                _versions.SetInstalled(package.Id, available); // 005 R5 icons
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
        _versions.Installed(package.Id) is { } available ? Compare(available, installedVersion) : VersionStatus.Unknown;

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
        catch (Exception ex) when (ex is not OperationCanceledException) // BR-12: whatever type the projection maps WinGet's code to
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
            var found = await connection.FindForDetailsAsync(package.Id, package.Source, package.IsInstalled, cancellationToken);
            var version = found.DefaultInstallVersion;
            if (version is null)
            {
                return null;
            }
            cancellationToken.ThrowIfCancellationRequested();
            var installed = found.InstalledVersion;
            return ToDetails(version.GetCatalogPackageMetadata(), ReadVersions(found, version, package.IsPrimaryMatch)) with
            {
                OffersScopeChoice = installed is null && OffersScopeChoice(version), // 015 R1.1
                InstalledScope = installed is null ? null : InstalledScopes.Read(installed, _installContext), // 015 R2.1
            };
        }, cancellationToken);
    }

    public Task<bool> OffersScopeChoiceAsync(PackageInfo package, string version, CancellationToken cancellationToken = default) =>
        !package.IsManaged ? Task.FromResult(false) : Task.Run(async () =>
        {
            try
            {
                var found = await connection.FindForDetailsAsync(package.Id, package.Source, installed: false, cancellationToken);
                var ids = found.AvailableVersions;
                for (var i = 0; i < ids.Count; i++)
                {
                    if (string.Equals(ids[i].Version, version, StringComparison.Ordinal))
                    {
                        return OffersScopeChoice(found.GetPackageVersionInfo(ids[i]));
                    }
                }
                return false;
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                return false;
            }
        }, cancellationToken);

    /// <summary>015 R1.2 for one version, through WinGet's own choice of installer per scope.</summary>
    private static bool OffersScopeChoice(PackageVersionInfo? version) => OffersScopeChoice(scope =>
    {
        try
        {
            return version?.GetApplicableInstaller(new InstallOptions { PackageInstallScope = scope })?.Scope;
        }
        catch (Exception)
        {
            return null;
        }
    });

    /// <summary>
    /// 015 R1.2: two real scopes, as WinGet itself would pick them. Asking for User also accepts an installer with no
    /// scope (portable, MSIX, Notepad++'s zip), so the returned installer's own scope is what counts (015 T1).
    /// </summary>
    internal static bool OffersScopeChoice(Func<PackageInstallScope, PackageInstallerScope?> pick) =>
        pick(PackageInstallScope.User) == PackageInstallerScope.User
        && pick(PackageInstallScope.System) == PackageInstallerScope.System;

    public async Task<string?> GetIconUrlAsync(PackageInfo package, CancellationToken cancellationToken = default)
    {
        if (_iconUrls.TryGetValue(package.Id, out var cached))
        {
            return cached;
        }
        if (_versions.Any(package.Id) is not { } version)
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
        _versions.ClearInstalled(); // the next load fills it again; search entries and icon URLs stay (BR-05)
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
                var answer = connection.AnswerFor(info.Id);
                var used = !info.Explicit || connection.IsSearched(info.Id); // 006 R3.14, R3.15
                var status = used ? AgreementStatus.Declined : AgreementStatus.None;
                List<Agreement> list = [];
                if (used && answer is not { Accepted: false }) // a declined source isn't contacted (R3.8), nor an unused explicit one (R3.14)
                {
                    try
                    {
                        list = CatalogConnection.ReadAgreements(reference);
                        status = SourceAgreementPolicy.Status(list, answer);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        status = AgreementStatus.Unknown;
                    }
                }
                sources.Add(new SourceInfo(
                    info.Id,
                    info.Name,
                    info.Type,
                    info.Argument,
                    info.TrustLevel == PackageCatalogTrustLevel.Trusted,
                    info.Explicit,
                    info.LastUpdateTime.Year > 1601 ? info.LastUpdateTime : null,
                    list,
                    status) { UsedInSearches = used });
            }
            return sources;
        }, cancellationToken);

    public async Task<string?> RefreshSourceAsync(string sourceName, CancellationToken cancellationToken = default)
    {
        var reference = await Task.Run(() => connection.Manager.GetPackageCatalogByName(sourceName), cancellationToken); // BR-16
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

    public IReadOnlyList<string> LeftOutSources => connection.LeftOutSources;

    public Task<SourceInfo> ReadSourceAgreementsAsync(SourceInfo source, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            try
            {
                return connection.Manager.GetPackageCatalogByName(source.Name) is { } reference
                    ? source with { Agreements = CatalogConnection.ReadAgreements(reference) }
                    : source;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return source;
            }
        }, cancellationToken);

    public void SetSourceAgreementAnswer(SourceInfo source, bool accepted) =>
        connection.SetSourceAgreementAnswer(source.Id, SourceAgreementPolicy.Hash(source.Agreements), accepted);

    public string GetWinGetVersion() => connection.Manager.Version;

    private List<PackageInfo> ToPackages(IReadOnlyList<MatchResult> matches)
    {
        var packages = new List<PackageInfo>(matches.Count);
        var versions = new List<KeyValuePair<string, PackageVersionInfo>>(matches.Count);
        for (var i = 0; i < matches.Count; i++)
        {
            var package = matches[i].CatalogPackage;
            var info = ToPackageInfo(package);
            if (package.DefaultInstallVersion is { } version)
            {
                versions.Add(new(info.Id, version));
            }
            packages.Add(info);
        }
        _versions.SetSearch(versions);
        return packages;
    }

    /// <summary>Prefers the theme-neutral icon; https only (005 R6.1).</summary>
    private static string? PickIcon(IReadOnlyList<Icon> icons)
    {
        string? fallback = null;
        for (var i = 0; i < icons.Count; i++)
        {
            var url = icons[i].Url;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
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
    /// direction with WinGet's comparer (P1) and never sort version strings ourselves. Each version's direction
    /// against the installed one also comes from WinGet's comparer (014 R1.2).
    /// </summary>
    private static List<PackageVersion> ReadVersions(CatalogPackage package, PackageVersionInfo reference, bool primaryMatch)
    {
        var ids = package.AvailableVersions;
        var versions = new List<string>(ids.Count);
        for (var i = 0; i < ids.Count; i++)
        {
            versions.Add(ids[i].Version);
        }
        if (versions.Count > 1 && SafeCompare(() => reference.CompareToVersion(versions[0])) == CompareResult.Greater)
        {
            versions.Reverse();
        }

        var installed = package.InstalledVersion;
        var list = new List<PackageVersion>(versions.Count);
        foreach (var v in versions)
        {
            CompareResult? compared = installed is not null && primaryMatch ? SafeCompare(() => installed.CompareToVersion(v)) : null;
            list.Add(new PackageVersion(v, Direction(compared, installed is not null, primaryMatch)));
        }
        return list;
    }

    /// <summary>BR-22: a version WinGet refuses to compare (an empty one, say) is Unknown, not a failed details load.</summary>
    internal static CompareResult? SafeCompare(Func<CompareResult> compare)
    {
        try
        {
            return compare();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>
    /// 014 R1.2: <paramref name="installedVsVersion"/> is the installed version compared with this one. A row WinGet didn't
    /// match itself (002 R6.4) never claims a direction.
    /// </summary>
    internal static VersionDirection Direction(CompareResult? installedVsVersion, bool installed, bool primaryMatch) =>
        !installed ? VersionDirection.NotInstalled
        : !primaryMatch ? VersionDirection.Unknown
        : installedVsVersion switch
        {
            CompareResult.Equal => VersionDirection.Installed,
            CompareResult.Greater => VersionDirection.Older,
            CompareResult.Lesser => VersionDirection.Newer,
            _ => VersionDirection.Unknown,
        };

    private static PackageDetails ToDetails(CatalogPackageMetadata metadata, IReadOnlyList<PackageVersion> versions)
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

    internal static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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

    public WinGetException(string message, int hresult)
        : base(message)
    {
        HResult = hresult;
    }

    /// <summary>The kind of failure one bad source can cause, so testing each source may get past it (011 R1.3).</summary>
    internal bool MayBeOneSource { get; init; }
}
