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
public sealed class WinGetPackageService : IPackageService
{
    private readonly PackageManager _packageManager = new();

    public async Task<IReadOnlyList<PackageInfo>> SearchAsync(string query, int maxResults = 50, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var catalog = await ConnectCompositeCatalogAsync(cancellationToken);

        var options = new FindPackagesOptions { ResultLimit = (uint)maxResults };
        options.Selectors.Add(new PackageMatchFilter
        {
            Field = PackageMatchField.CatalogDefault,
            Option = PackageFieldMatchOption.ContainsCaseInsensitive,
            Value = query.Trim(),
        });

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

    /// <summary>
    /// Connects to every remote source merged with the local (installed) catalog,
    /// so results carry both installed and available versions.
    /// </summary>
    private async Task<PackageCatalog> ConnectCompositeCatalogAsync(CancellationToken cancellationToken)
    {
        var options = new CreateCompositePackageCatalogOptions
        {
            CompositeSearchBehavior = CompositeSearchBehavior.RemotePackagesFromAllCatalogs,
        };
        var catalogs = _packageManager.GetPackageCatalogs();
        for (var i = 0; i < catalogs.Count; i++)
        {
            options.Catalogs.Add(catalogs[i]);
        }

        var composite = _packageManager.CreateCompositePackageCatalog(options);
        var connect = await composite.ConnectAsync().AsTask(cancellationToken);
        if (connect.Status != ConnectResultStatus.Ok)
        {
            throw new WinGetException(DescribeConnectFailure(connect.Status), connect.ExtendedErrorCode);
        }

        return connect.PackageCatalog;
    }

    // User-facing messages (spec 001 R3.4). The HRESULT stays on WinGetException.HResult.
    internal static string DescribeConnectFailure(ConnectResultStatus status) => status switch
    {
        ConnectResultStatus.CatalogError =>
            "Couldn't reach the package sources. Check your internet connection and try again.",
        ConnectResultStatus.SourceAgreementsNotAccepted =>
            "A package source requires you to accept its agreement. Run 'winget search' once in a terminal to accept it, then try again.",
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
            Source: available?.PackageCatalog.Info.Name ?? installed?.PackageCatalog.Info.Name ?? "",
            UpdateAvailable: package.IsUpdateAvailable);
    }
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
