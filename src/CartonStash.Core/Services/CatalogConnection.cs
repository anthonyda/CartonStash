using System.Collections.Concurrent;
using Microsoft.Management.Deployment;
using CartonStash.Core.Models;

namespace CartonStash.Core.Services;

/// <summary>
/// Owns the WinGet <see cref="PackageManager"/> and caches the two connected composite catalogs
/// (spec 002): <em>remote-first</em> for search and <em>local-first</em> for the installed list.
/// Call <see cref="Invalidate"/> after anything that changes the system or the sources.
/// </summary>
/// <remarks>One instance per app (singleton). Shared by every WinGet service so they reuse connections.</remarks>
public sealed class CatalogConnection(ISettings settings)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ConcurrentDictionary<string, SourceFailure> _failedSources = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _leftOutSources = new(StringComparer.OrdinalIgnoreCase);
    private PackageCatalog? _remoteFirst;
    private PackageCatalog? _localFirst;
    private PackageCatalog? _installedOnly;

    internal PackageManager Manager { get; } = new();

    /// <summary>Sources left out of the catalogs until the next <see cref="Invalidate"/> (011 R1.5).</summary>
    public IReadOnlyList<SourceFailure> SourceFailures => [.. _failedSources.Values.OrderBy(f => f.Source, StringComparer.OrdinalIgnoreCase)];

    /// <summary>Sources left out of the last connection because their agreements aren't accepted (006 R3.8).</summary>
    public IReadOnlyList<string> LeftOutSources => [.. _leftOutSources.Keys.Order(StringComparer.OrdinalIgnoreCase)];

    /// <summary>Drops the cached catalogs; the next call reconnects, sees fresh data and tries every source again.</summary>
    public void Invalidate()
    {
        _failedSources.Clear();
        _leftOutSources.Clear();
        _remoteFirst = null;
        _localFirst = null;
        _installedOnly = null;
    }

    /// <summary>Leaves these sources out of the next connection. Memory only: the user's sources stay as they are (011 R1.6).</summary>
    internal void LeaveOut(IReadOnlyList<SourceFailure> failures)
    {
        foreach (var failure in failures)
        {
            _failedSources[failure.Source] = failure;
        }
        _remoteFirst = null;
        _localFirst = null;
    }

    /// <summary>Saves the user's answer for this agreement text and reconnects with it (006 R3.9).</summary>
    public void SetSourceAgreementAnswer(string sourceId, string textHash, bool accepted)
    {
        settings.Update(s => s with
        {
            SourceAgreementAnswers = new Dictionary<string, SourceAgreementAnswer>(s.SourceAgreementAnswers)
            {
                [sourceId] = new(accepted, textHash),
            },
        });
        Invalidate();
    }

    internal SourceAgreementAnswer? AnswerFor(string sourceId) => settings.Current.SourceAgreementAnswers.GetValueOrDefault(sourceId);

    /// <summary>006 R3.15: the explicit source's <b>Use in searches</b> box.</summary>
    internal bool IsSearched(string sourceId) => settings.Current.IsSearched(sourceId);

    /// <summary>The source's agreements, read by index (W2).</summary>
    internal static List<Agreement> ReadAgreements(PackageCatalogReference reference)
    {
        var agreements = reference.SourceAgreements;
        var list = new List<Agreement>(agreements.Count);
        for (var i = 0; i < agreements.Count; i++)
        {
            list.Add(new Agreement(agreements[i].Label, WinGetPackageService.NullIfEmpty(agreements[i].Text), WinGetPackageService.NullIfEmpty(agreements[i].Url)));
        }
        return list;
    }

    /// <summary>
    /// The installed-only catalog: one package per installed program, not merged by package ID (002 R6.1).
    /// </summary>
    internal async Task<PackageCatalog> GetInstalledOnlyAsync(CancellationToken cancellationToken)
    {
        if (_installedOnly is { } cached)
        {
            return cached;
        }

        // A rare double connect while two loads race is harmless; the last one is kept.
        var reference = Manager.GetLocalPackageCatalog(LocalPackageCatalog.InstalledPackages);
        var connect = await reference.ConnectAsync().AsTask(cancellationToken);
        if (connect.Status != ConnectResultStatus.Ok)
        {
            throw new WinGetException(WinGetPackageService.DescribeConnectFailure(connect.Status), connect.ExtendedErrorCode);
        }
        return _installedOnly = connect.PackageCatalog;
    }

    /// <param name="localFirst">
    /// True: the installed catalog correlated with the remote sources; results are installed packages (002).
    /// False: the remote sources merged with the installed catalog; results are remote packages (001).
    /// </param>
    /// <returns>Null when no source may be used and <paramref name="localFirst"/> is false (006 R3.10).</returns>
    private async Task<PackageCatalog?> GetAsync(bool localFirst, CancellationToken cancellationToken)
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

            var catalog = await ConnectAsync(localFirst, IsInUse, cancellationToken);
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

    private bool IsInUse(string sourceName) => !_failedSources.ContainsKey(sourceName) && !_leftOutSources.ContainsKey(sourceName);

    /// <summary>
    /// 006 R3.14, R3.15: an explicit source counts only for search (the remote-first catalog), and only with its
    /// <b>Use in searches</b> box checked. The installed list, updates and background checks leave it out, as <c>winget</c>.
    /// </summary>
    internal static bool Counts(bool isExplicit, string sourceId, bool localFirst, AppSettings settings) =>
        !isExplicit || (!localFirst && settings.IsSearched(sourceId));

    private async Task<PackageCatalog?> ConnectAsync(bool localFirst, Func<string, bool> include, CancellationToken cancellationToken)
    {
        var options = new CreateCompositePackageCatalogOptions
        {
            CompositeSearchBehavior = localFirst
                ? CompositeSearchBehavior.LocalCatalogs
                : CompositeSearchBehavior.RemotePackagesFromAllCatalogs,
        };
        // Off the UI thread: each read is a call into WinGet's process (A4).
        await Task.Run(() =>
        {
            var catalogs = Manager.GetPackageCatalogs();
            for (var i = 0; i < catalogs.Count; i++)
            {
                var reference = catalogs[i];
                if (!include(reference.Info.Name) || !Counts(reference.Info.Explicit, reference.Info.Id, localFirst, settings.Current))
                {
                    continue; // R3.14: an explicit source not used here isn't even read
                }
                var name = reference.Info.Name;
                if (AnswerFor(reference.Info.Id) is { Accepted: false })
                {
                    _leftOutSources[name] = 0; // not even read: reading reaches the source (R3.8)
                    continue;
                }

                List<Agreement> agreements;
                try
                {
                    agreements = ReadAgreements(reference);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // The text comes from the source. Unread means not accepted, and the others carry on (R3.8, 011 R1.1).
                    _failedSources[name] = new SourceFailure(name, SourceFailureMessages.Describe(name, ex.HResult, WindowsRegion.IsCountry()), ex.HResult);
                    continue;
                }

                if (agreements.Count > 0)
                {
                    // Not set to false: one refused reference fails the whole composite, so it's left out instead (R3.8).
                    if (!SourceAgreementPolicy.MayUse(AnswerFor(reference.Info.Id), SourceAgreementPolicy.Hash(agreements)))
                    {
                        _leftOutSources[name] = 0;
                        continue;
                    }
                    reference.AcceptSourceAgreements = true;
                }
                options.Catalogs.Add(reference);
            }
        }, cancellationToken);
        if (options.Catalogs.Count == 0)
        {
            return localFirst ? await GetInstalledOnlyAsync(cancellationToken) : null; // R3.10
        }
        // A composite always includes the installed catalog; adding it explicitly makes WinGet reject the
        // search with InvalidOptions. CompositeSearchBehavior decides which side the results come from.

        var composite = Manager.CreateCompositePackageCatalog(options);
        var connect = await composite.ConnectAsync().AsTask(cancellationToken);
        if (connect.Status != ConnectResultStatus.Ok)
        {
            throw new WinGetException(WinGetPackageService.DescribeConnectFailure(connect.Status), connect.ExtendedErrorCode)
            {
                MayBeOneSource = connect.Status == ConnectResultStatus.CatalogError,
            };
        }
        return connect.PackageCatalog;
    }

    /// <summary>
    /// Searches a composite catalog; a source that fails is left out and named, and the others still answer (011 R1.1, R1.2).
    /// </summary>
    /// <param name="options">A factory: testing a source needs its own copy of the request.</param>
    internal Task<IReadOnlyList<MatchResult>> FindAsync(bool localFirst, Func<FindPackagesOptions> options, CancellationToken cancellationToken) =>
        SourceIsolation.RunAsync(
            async () => await GetAsync(localFirst, cancellationToken) is { } catalog
                ? await FindMatchesAsync(catalog, options(), cancellationToken)
                : [],
            () => SourcesInUse(localFirst),
            () => SourceFailures,
            source => Task.Run(() => ProbeAsync(source, localFirst, options, cancellationToken), cancellationToken),
            (source, code) => new SourceFailure(source, SourceFailureMessages.Describe(source, code, WindowsRegion.IsCountry()), code),
            LeaveOut);

    private List<string> SourcesInUse(bool localFirst)
    {
        var catalogs = Manager.GetPackageCatalogs();
        var names = new List<string>(catalogs.Count);
        for (var i = 0; i < catalogs.Count; i++)
        {
            var info = catalogs[i].Info;
            var name = info.Name;
            if (IsInUse(name) && Counts(info.Explicit, info.Id, localFirst, settings.Current)) // 011 R1.3: never test a source not used
            {
                names.Add(name);
            }
        }
        return names;
    }

    /// <summary>Runs the request on one source alone. Returns WinGet's code, or null when the source works.</summary>
    private async Task<int?> ProbeAsync(string source, bool localFirst, Func<FindPackagesOptions> options, CancellationToken cancellationToken)
    {
        try
        {
            if (await ConnectAsync(localFirst, name => string.Equals(name, source, StringComparison.OrdinalIgnoreCase), cancellationToken) is { } catalog)
            {
                await FindMatchesAsync(catalog, options(), cancellationToken);
            }
            return null;
        }
        catch (WinGetException ex) when (ex.MayBeOneSource)
        {
            return ex.HResult;
        }
    }

    internal static async Task<IReadOnlyList<MatchResult>> FindMatchesAsync(PackageCatalog catalog, FindPackagesOptions options, CancellationToken cancellationToken)
    {
        var result = await catalog.FindPackagesAsync(options).AsTask(cancellationToken);
        if (result.Status != FindPackagesResultStatus.Ok)
        {
            throw new WinGetException(WinGetPackageService.DescribeFindFailure(result.Status), result.ExtendedErrorCode)
            {
                MayBeOneSource = result.Status is FindPackagesResultStatus.CatalogError or FindPackagesResultStatus.InternalError, // R1.3
            };
        }
        return result.Matches;
    }

    /// <summary>Finds one installed program by its installed entry ID (<c>ARP\...</c>, <c>MSIX\...</c>) (002 R6.5).</summary>
    internal async Task<CatalogPackage> FindInstalledEntryAsync(string installedId, CancellationToken cancellationToken)
    {
        var catalog = await GetInstalledOnlyAsync(cancellationToken);
        var matches = await FindMatchesAsync(catalog, ById(installedId), cancellationToken);
        return matches.Count > 0
            ? matches[0].CatalogPackage
            : throw new WinGetException("This program is no longer installed. Refresh the list and try again.", null);
    }

    /// <summary>
    /// Finds one package by exact ID (003: COM objects never leave Services). With a source, only that source counts: the
    /// user chose the package from it, so the same ID in another source is not a substitute (003 R1.2).
    /// </summary>
    internal async Task<CatalogPackage> FindByIdAsync(string id, string? source, bool localFirst, CancellationToken cancellationToken)
    {
        return await FindByIdInCatalogAsync(localFirst, id, source, cancellationToken) ?? throw NotFound(id, source);
    }

    /// <summary>
    /// Finds a package to read its details (005 R1.2). WinGet's full installed list links some programs to a source that a
    /// search by ID on the local-first catalog can't find (seen with Microsoft.PowerShell), so this also looks in the source
    /// itself. Read-only: operations use <see cref="FindByIdAsync"/>, which only acts on what WinGet can match.
    /// </summary>
    internal async Task<CatalogPackage> FindForDetailsAsync(string id, string? source, bool installed, CancellationToken cancellationToken)
    {
        var found = installed ? await FindByIdInCatalogAsync(localFirst: true, id, source, cancellationToken) : null;
        return found ?? await FindByIdInCatalogAsync(localFirst: false, id, source, cancellationToken) ?? throw NotFound(id, source);
    }

    private async Task<CatalogPackage?> FindByIdInCatalogAsync(bool localFirst, string id, string? source, CancellationToken cancellationToken)
    {
        var matches = await FindAsync(localFirst, () => ById(id), cancellationToken);
        for (var i = 0; i < matches.Count; i++)
        {
            var package = matches[i].CatalogPackage;
            if (IsFromSource(source, package.DefaultInstallVersion?.PackageCatalog.Info.Name))
            {
                return package;
            }
        }
        return null;
    }

    /// <summary>
    /// 014 R3.9: how many installed programs WinGet matches to the package ID, as the Installed page lists them (one row
    /// each, 002 R6.1). By index (W2).
    /// </summary>
    internal async Task<int> CountInstalledAsync(string id, CancellationToken cancellationToken)
    {
        var matches = await FindAsync(localFirst: true, () => ById(id), cancellationToken);
        var count = 0;
        for (var i = 0; i < matches.Count; i++)
        {
            if (matches[i].CatalogPackage.InstalledVersion is not null)
            {
                count++;
            }
        }
        return count;
    }

    private static FindPackagesOptions ById(string id)
    {
        var options = new FindPackagesOptions();
        options.Selectors.Add(new PackageMatchFilter
        {
            Field = PackageMatchField.Id,
            Option = PackageFieldMatchOption.Equals,
            Value = id,
        });
        return options;
    }

    private WinGetException NotFound(string id, string? source) =>
        LeftOutSourceError(source, LeftOutSources)
        ?? FailedSourceError(id, source, SourceFailures)
        ?? new($"WinGet can no longer find the package '{id}'. Refresh and try again.", null);

    /// <summary>A package from a source not in use isn't gone: say how to use it (006 R3.10). Null when that isn't the case.</summary>
    internal static WinGetException? LeftOutSourceError(string? source, IReadOnlyList<string> leftOut) =>
        leftOut.Contains(source, StringComparer.OrdinalIgnoreCase)
            ? new($"'{source}' isn't in use. Read and accept its agreements in Settings, then try again.", null)
            : null;

    /// <summary>A package from a failed source isn't gone: say which source failed (011 R2.6). Null when none is involved.</summary>
    internal static WinGetException? FailedSourceError(string id, string? source, IReadOnlyList<SourceFailure> failures)
    {
        var relevant = failures.Where(f => string.IsNullOrEmpty(source) || string.Equals(f.Source, source, StringComparison.OrdinalIgnoreCase)).ToList();
        return relevant.Count == 0 ? null : new($"Couldn't look up '{id}'. {SourceFailureMessages.Join(relevant)}", relevant[0].HResult);
    }

    internal static bool IsFromSource(string? requested, string? actual) =>
        string.IsNullOrEmpty(requested) || actual == requested;
}
