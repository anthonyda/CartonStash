using Microsoft.Management.Deployment;

namespace CartonStash.Core.Services;

/// <summary>
/// Owns the WinGet <see cref="PackageManager"/> and caches the two connected composite catalogs
/// (spec 002): <em>remote-first</em> for search and <em>local-first</em> for the installed list.
/// Call <see cref="Invalidate"/> after anything that changes the system or the sources.
/// </summary>
/// <remarks>One instance per app (singleton). Shared by every WinGet service so they reuse connections.</remarks>
public sealed class CatalogConnection
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly HashSet<string> _acceptedSourceAgreements = new(StringComparer.OrdinalIgnoreCase);
    private PackageCatalog? _remoteFirst;
    private PackageCatalog? _localFirst;

    internal PackageManager Manager { get; } = new();

    /// <summary>Drops the cached catalogs; the next call reconnects and sees fresh data.</summary>
    public void Invalidate()
    {
        _remoteFirst = null;
        _localFirst = null;
    }

    /// <summary>The user accepted this source's agreements for this session (006 R3.3).</summary>
    public void AcceptSourceAgreements(string sourceName)
    {
        lock (_acceptedSourceAgreements)
        {
            _acceptedSourceAgreements.Add(sourceName);
        }
        Invalidate();
    }

    internal bool HasAcceptedSourceAgreements(string sourceName)
    {
        lock (_acceptedSourceAgreements)
        {
            return _acceptedSourceAgreements.Contains(sourceName);
        }
    }

    /// <summary>All remote sources merged with the installed catalog; results are remote packages (001).</summary>
    internal Task<PackageCatalog> GetRemoteFirstAsync(CancellationToken cancellationToken) =>
        GetAsync(localFirst: false, cancellationToken);

    /// <summary>The installed catalog correlated with all remote sources; results are installed packages (002).</summary>
    internal Task<PackageCatalog> GetLocalFirstAsync(CancellationToken cancellationToken) =>
        GetAsync(localFirst: true, cancellationToken);

    private async Task<PackageCatalog> GetAsync(bool localFirst, CancellationToken cancellationToken)
    {
        var cached = localFirst ? _localFirst : _remoteFirst;
        if (cached is not null)
        {
            return cached;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            cached = localFirst ? _localFirst : _remoteFirst;
            if (cached is not null)
            {
                return cached;
            }

            var catalog = await ConnectAsync(localFirst, cancellationToken);
            if (localFirst)
            {
                _localFirst = catalog;
            }
            else
            {
                _remoteFirst = catalog;
            }
            return catalog;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<PackageCatalog> ConnectAsync(bool localFirst, CancellationToken cancellationToken)
    {
        var options = new CreateCompositePackageCatalogOptions
        {
            CompositeSearchBehavior = localFirst
                ? CompositeSearchBehavior.LocalCatalogs
                : CompositeSearchBehavior.RemotePackagesFromAllCatalogs,
        };
        var catalogs = Manager.GetPackageCatalogs();
        for (var i = 0; i < catalogs.Count; i++)
        {
            var reference = catalogs[i];
            if (HasAcceptedSourceAgreements(reference.Info.Name))
            {
                reference.AcceptSourceAgreements = true;
            }
            options.Catalogs.Add(reference);
        }
        // A composite always includes the installed catalog; adding it explicitly makes WinGet reject the
        // search with InvalidOptions. CompositeSearchBehavior decides which side the results come from.

        var composite = Manager.CreateCompositePackageCatalog(options);
        var connect = await composite.ConnectAsync().AsTask(cancellationToken);
        if (connect.Status != ConnectResultStatus.Ok)
        {
            throw new WinGetException(WinGetPackageService.DescribeConnectFailure(connect.Status), connect.ExtendedErrorCode);
        }
        return connect.PackageCatalog;
    }

    /// <summary>Finds one package by exact ID, optionally restricted to a source (003: COM objects never leave Services).</summary>
    internal async Task<CatalogPackage> FindByIdAsync(string id, string? source, bool localFirst, CancellationToken cancellationToken)
    {
        var catalog = localFirst ? await GetLocalFirstAsync(cancellationToken) : await GetRemoteFirstAsync(cancellationToken);
        var options = new FindPackagesOptions();
        options.Selectors.Add(new PackageMatchFilter
        {
            Field = PackageMatchField.Id,
            Option = PackageFieldMatchOption.Equals,
            Value = id,
        });

        var result = await catalog.FindPackagesAsync(options).AsTask(cancellationToken);
        if (result.Status != FindPackagesResultStatus.Ok)
        {
            throw new WinGetException(WinGetPackageService.DescribeFindFailure(result.Status), result.ExtendedErrorCode);
        }

        var matches = result.Matches;
        CatalogPackage? first = null;
        for (var i = 0; i < matches.Count; i++)
        {
            var package = matches[i].CatalogPackage;
            first ??= package;
            if (string.IsNullOrEmpty(source) || package.DefaultInstallVersion?.PackageCatalog.Info.Name == source)
            {
                return package;
            }
        }
        return first ?? throw new WinGetException($"WinGet can no longer find the package '{id}'. Refresh and try again.", null);
    }
}
