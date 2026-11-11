using Microsoft.Management.Deployment;
using Windows.Foundation;
using CartonStash.Core.Models;

namespace CartonStash.Core.Services;

/// <summary>
/// <see cref="IPackageInstaller"/> on the WinGet COM API (specs 003, 004). WinGet starts the installer, and the
/// installer asks for UAC itself when it needs to; the app never elevates (003 R6.1, constitution A5).
/// </summary>
/// <param name="installContext">Windows Installer's context of MSI products (014 R3.8); <see cref="MsiInstallContext"/> when null.</param>
public sealed class WinGetPackageInstaller(CatalogConnection connection, ISettings settings, IRunningPrograms runningPrograms,
    IInstallContext? installContext = null) : IPackageInstaller
{
    private readonly IInstallContext _installContext = installContext ?? new MsiInstallContext();

    public async Task<IReadOnlyList<Agreement>> GetAgreementsAsync(PackageInfo package, string? version = null, CancellationToken cancellationToken = default)
    {
        var found = await connection.FindByIdAsync(package.Id, package.Source, localFirst: false, cancellationToken);
        // 014 R2.2: the chosen version's agreements. A version the source no longer has has none here; the run says so.
        var info = version is null ? found.DefaultInstallVersion
            : FindVersion(found, version) is { } id ? found.GetPackageVersionInfo(id) : null;
        if (info is null)
        {
            return [];
        }

        return await Task.Run<IReadOnlyList<Agreement>>(() =>
        {
            var agreements = info.GetCatalogPackageMetadata().Agreements;
            var list = new List<Agreement>(agreements.Count);
            for (var i = 0; i < agreements.Count; i++)
            {
                var a = agreements[i];
                list.Add(new Agreement(
                    string.IsNullOrWhiteSpace(a.Label) ? "Agreement" : a.Label,
                    string.IsNullOrWhiteSpace(a.Text) ? null : a.Text,
                    string.IsNullOrWhiteSpace(a.Url) ? null : a.Url));
            }
            return list;
        }, cancellationToken);
    }

    public async Task<OperationOutcome> RunAsync(OperationKind kind, PackageInfo package, bool acceptAgreements, string? version, string? logPath,
        IProgress<OperationProgress> progress, CancellationToken cancellationToken, InstallScope? scope = null, ScopeSource scopeSource = ScopeSource.Installed)
    {
        // Re-find the package; COM objects never leave this class (A2).
        var found = UninstallsInstalledEntry(kind, package)
            ? await connection.FindInstalledEntryAsync(package.InstalledId!, cancellationToken)
            : await connection.FindByIdAsync(package.Id, package.Source, localFirst: kind != OperationKind.Install, cancellationToken);
        try
        {
            // 014 R3.9: going back counts the installed entries before and after, to catch a second copy.
            var goingBack = GoesBack(kind, package, version);
            // 015 R3.1: an update may run as an install, which skips uninstallPrevious, so it gets the MSI half of the check.
            var updating = kind == OperationKind.Upgrade && package.IsInstalled;
            var entriesBefore = goingBack ? await CountInstalledAsync(package.Id) : null;
            var msiBefore = goingBack || updating ? await Task.Run(() => MsiProductsOf(found.InstalledVersion), cancellationToken) : [];
            var outcome = kind switch
            {
                OperationKind.Uninstall => await UninstallAsync(found, package.Name, logPath, progress, cancellationToken),
                _ => await InstallAsync(kind, found, package, acceptAgreements, version, scope, scopeSource, logPath, progress, cancellationToken),
            };
            if (goingBack && outcome.Result == OperationResult.Succeeded)
            {
                connection.Invalidate();
                var stillInstalled = await Task.Run(() => AnyStillInstalled(msiBefore, ChosenProductCodes(found, version!)), CancellationToken.None);
                outcome = AfterGoingBack(outcome, entriesBefore, await CountInstalledAsync(package.Id), stillInstalled, package.Name, version!, package.InstalledVersion);
            }
            else if (updating && msiBefore.Count > 0 && outcome.Result == OperationResult.Succeeded)
            {
                var target = version ?? found.DefaultInstallVersion?.Version;
                if (target is not null)
                {
                    var stillInstalled = await Task.Run(() => AnyStillInstalled(msiBefore, ChosenProductCodes(found, target)), CancellationToken.None);
                    outcome = AfterGoingBack(outcome, null, null, stillInstalled, package.Name, target, package.InstalledVersion);
                }
            }
            switch (CheckAfter(kind, outcome, package, version))
            {
                case CheckAfterOperation.AppInstaller:
                    connection.Invalidate();
                    outcome = AfterAppInstallerUpdate(outcome, await OldVersionStillInstalledAsync(package));
                    break;
                case CheckAfterOperation.InstalledVersion:
                    connection.Invalidate();
                    outcome = AfterUpdate(outcome, package.InstalledVersion, await InstalledAfterUpdateAsync(package), package.Name);
                    break;
            }
            return outcome;
        }
        finally
        {
            // The system changed (or may have): next reads must not use stale catalogs (002 R1.3).
            connection.Invalidate();
        }
    }

    private async Task<OperationOutcome> InstallAsync(OperationKind kind, CatalogPackage package, PackageInfo info, bool acceptAgreements, string? version,
        InstallScope? explicitScope, ScopeSource explicitSource, string? logPath, IProgress<OperationProgress> progress, CancellationToken cancellationToken)
    {
        var name = info.Name;
        // 014 R3.8, 015 R3.1: going back and updates ask for the scope the package is installed with; anything else uses
        // the setting (006 R2.1).
        var goingBack = GoesBack(kind, info, version);
        var keepsInstalled = goingBack || (kind == OperationKind.Upgrade && info.IsInstalled);
        var installed = package.InstalledVersion;
        // One read of Windows Installer's context serves both 014 R3.8 and 003 R8.1.
        var msiContext = installed is null ? null : await Task.Run(() => InstalledScopes.MsiContextOf(installed, _installContext), cancellationToken);
        var (scope, scopeSource) = keepsInstalled && explicitScope is null
            ? await Task.Run(() => ScopeFor(null, true, msiContext, InstalledScopes.WinGetScopeOf(installed), settings.Current.InstallScope), cancellationToken)
            : ScopeFor(explicitScope, keepsInstalled, null, null, settings.Current.InstallScope, explicitSource);
        // 015 R3.1: an update keeps the installed scope only if the version it installs has an installer for it.
        if (kind == OperationKind.Upgrade && scopeSource == ScopeSource.Installed && explicitScope is null
            && !await Task.Run(() => HasInstallerFor(TargetVersion(package, version), scope), cancellationToken))
        {
            (scope, scopeSource) = (settings.Current.InstallScope, ScopeSource.Setting);
        }
        var mode = ModeFor(kind, installed is not null, msiContext);
        var options = new InstallOptions
        {
            PackageInstallMode = mode == OperationMode.DefaultForAllUsersMsi ? PackageInstallMode.Default : PackageInstallMode.Silent, // 003 R8.1
            PackageInstallScope = ToWinGet(scope),
            AcceptPackageAgreements = acceptAgreements,
            // 014 T1: Force never helped an older version go back, and it lets other warnings through.
        };
        if (logPath is not null)
        {
            options.LogOutputPath = logPath;
        }
        if (version is not null)
        {
            // 014 R2.1: looked up again now, by index (W2); the source may have changed since the pane opened.
            if (FindVersion(package, version) is not { } id)
            {
                return VersionUnavailable(info.Source) with { Scope = scope, ScopeSource = scopeSource, Mode = mode };
            }
            options.PackageVersionId = id;
        }

        // 003 R8.3; 015 R1.4: an install for all users the user chose may ask too (Inno and others elevate themselves).
        if (mode == OperationMode.DefaultForAllUsersMsi || (scopeSource == ScopeSource.Chosen && scope == InstallScope.System))
        {
            progress.Report(new OperationProgress(OperationPhase.Queued) { MayAskPermission = true });
        }
        // 015 R3.1: WinGet's upgrade keeps the scope it reads; when Windows Installer says otherwise, install the new
        // version in Windows Installer's scope (measured in T9: the upgrade found no installer, the install replaced it).
        var byInstalling = scopeSource == ScopeSource.Installed
            && UpdatesByInstalling(kind, msiContext, InstalledScopes.FromWinGetScope(InstalledScopes.WinGetScopeOf(installed)));
        if (byInstalling && options.PackageVersionId is null && TargetVersionId(package) is { } defaultId)
        {
            options.PackageVersionId = defaultId;
        }
        var operation = kind == OperationKind.Upgrade && !byInstalling
            ? connection.Manager.UpgradePackageAsync(package, options)
            : connection.Manager.InstallPackageAsync(package, options);
        var winGetProgress = new Progress<InstallProgress>(p => progress.Report(MapInstallProgress(p)));
        var result = await operation.AsTask(cancellationToken, winGetProgress);

        var extendedError = result.ExtendedErrorCode?.HResult ?? 0;
        var running = await RunningFilesAfterAsync(extendedError, package);
        var outcome = MapInstallResult(kind, result.Status, result.InstallerErrorCode, result.RebootRequired, settings.Current.InstallScope,
            extendedError, name, running, goingBackTo: goingBack ? version : null, installedVersion: info.InstalledVersion, mode: mode)
            with { Details = FormatDetails(result.Status.ToString(), result.InstallerErrorCode, result.ExtendedErrorCode), Scope = scope, ScopeSource = scopeSource, Mode = mode };
        // 015 R3.2: an update that kept the installed scope against a scope chosen in Settings says so.
        return kind == OperationKind.Upgrade && outcome.Result == OperationResult.Succeeded
            && UpdatedScopeNote(name, settings.Current.InstallScope, scope, scopeSource) is { } note
            ? outcome with { Message = note }
            : outcome;
    }

    /// <summary>
    /// 015 R3.1: an update runs as an install when Windows Installer knows the installed version's scope and WinGet reads
    /// it differently (T9: Pandoc per-user, which WinGet reads as Machine, so its upgrade found no per-user installer).
    /// </summary>
    internal static bool UpdatesByInstalling(OperationKind kind, InstallScope? msiContext, InstallScope? winGetScope) =>
        kind == OperationKind.Upgrade && msiContext is InstallScope.User or InstallScope.System && winGetScope is { } w && w != msiContext;

    /// <summary>The default version's ID, by index (W2): what an update by install puts in <c>PackageVersionId</c>.</summary>
    private static PackageVersionId? TargetVersionId(CatalogPackage package)
    {
        try
        {
            var target = package.DefaultInstallVersion?.Version;
            return target is null ? null : FindVersion(package, target);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>The version an operation installs: the chosen one, else WinGet's default.</summary>
    private static PackageVersionInfo? TargetVersion(CatalogPackage package, string? version)
    {
        try
        {
            return version is not null && FindVersion(package, version) is { } id ? package.GetPackageVersionInfo(id) : package.DefaultInstallVersion;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>015 R3.1: WinGet has an installer of that scope (or one without a scope) for the version. A failed read is no.</summary>
    private static bool HasInstallerFor(PackageVersionInfo? info, InstallScope scope)
    {
        try
        {
            var picked = info?.GetApplicableInstaller(new InstallOptions { PackageInstallScope = ToWinGet(scope) });
            return InstallerFits(picked?.Scope, scope);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>015 R3.1: the installer WinGet picked fits the scope: the same, or one with no scope (portable, MSIX).</summary>
    internal static bool InstallerFits(PackageInstallerScope? picked, InstallScope scope) => picked switch
    {
        null => false,
        PackageInstallerScope.Unknown => true,
        PackageInstallerScope.User => scope == InstallScope.User,
        PackageInstallerScope.System => scope == InstallScope.System,
        _ => false,
    };

    /// <summary>
    /// 015 R3.2, Q2: "Updated for the current user, as Pandoc is installed." when the update used the installed scope and
    /// Settings names the other one. Nothing for "Let WinGet decide", when they match, or when the scope wasn't the
    /// installed one.
    /// </summary>
    internal static string? UpdatedScopeNote(string packageName, InstallScope setting, InstallScope used, ScopeSource source)
    {
        if (source != ScopeSource.Installed || setting == InstallScope.Any || used is not (InstallScope.User or InstallScope.System) || used == setting)
        {
            return null;
        }
        var name = string.IsNullOrWhiteSpace(packageName) ? "the package" : packageName;
        var who = used == InstallScope.System ? "all users" : "the current user";
        return $"Updated for {who}, as {name} is installed.";
    }

    /// <summary>R3.9: the installed version's MSI product codes that Windows Installer has (W2: by index). Empty for anything else.</summary>
    private List<string> MsiProductsOf(PackageVersionInfo? installed)
    {
        var found = new List<string>();
        try
        {
            var codes = installed?.ProductCodes;
            for (var i = 0; codes is not null && i < codes.Count; i++)
            {
                if (_installContext.ContextOf(codes[i]) is not null)
                {
                    found.Add(codes[i]);
                }
            }
        }
        catch (Exception)
        {
            // no codes: R3.9 falls back to the count
        }
        return found;
    }

    /// <summary>R3.9: the chosen version's own product codes, from its manifest; empty when they can't be read.</summary>
    private static List<string> ChosenProductCodes(CatalogPackage package, string version)
    {
        var codes = new List<string>();
        try
        {
            if (FindVersion(package, version) is { } id && package.GetPackageVersionInfo(id)?.ProductCodes is { } list)
            {
                for (var i = 0; i < list.Count; i++)
                {
                    codes.Add(list[i]);
                }
            }
        }
        catch (Exception)
        {
        }
        return codes;
    }

    /// <summary>R3.9: one of the old version's MSI products is still installed, and the chosen version isn't that same product.</summary>
    private bool AnyStillInstalled(IReadOnlyList<string> before, IReadOnlyList<string> chosen) =>
        StillInstalled(before, chosen, code => _installContext.ContextOf(code) is not null);

    internal static bool StillInstalled(IReadOnlyList<string> before, IReadOnlyList<string> chosen, Func<string, bool> isInstalled)
    {
        try
        {
            return before.Any(code => !chosen.Contains(code, StringComparer.OrdinalIgnoreCase) && isInstalled(code));
        }
        catch (Exception)
        {
            return false; // never an error of its own
        }
    }

    /// <summary>R3.9's count; null when it can't be read (a failed check is never an error of its own, 013 R2.3).</summary>
    private async Task<int?> CountInstalledAsync(string id)
    {
        try
        {
            return await connection.CountInstalledAsync(id, CancellationToken.None);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// 014 R3.9: going back reported Ok but left more installed entries than before: the chosen version went in next to
    /// the installed one, not over it (seen with Pandoc's per-user and all-users MSIs). A warning, so no exclusion (R3.2).
    /// Otherwise, or when either count is unknown, the outcome unchanged.
    /// </summary>
    internal static OperationOutcome AfterGoingBack(OperationOutcome outcome, int? before, int? after, bool previousStillInstalled,
        string packageName, string version, string? installed)
    {
        var moreEntries = before is { } b && after is { } a && a > b;
        if (outcome.Result != OperationResult.Succeeded || !(moreEntries || previousStillInstalled))
        {
            return outcome;
        }
        var name = string.IsNullOrWhiteSpace(packageName) ? "The package" : packageName;
        var old = installed is null ? "the installed version" : installed;
        return outcome with
        {
            Result = OperationResult.SucceededUnconfirmed,
            Message = $"{name} {version} was installed next to {old}, not over it. Both are now installed. "
                + $"To keep only {version}, uninstall {name} {old} from the Installed page.",
        };
    }

    /// <summary>
    /// 014 R3.8: the scope to ask WinGet for, and whether it's how the package is installed. An explicit scope (carried
    /// across the R3.5 uninstall) comes first; then, only when going back or updating (015 R3.1), Windows Installer's context of the product,
    /// then WinGet's <c>InstalledScope</c>; then the setting. T18: WinGet's scope is wrong for some MSIs, so Windows
    /// Installer comes before it.
    /// </summary>
    internal static (InstallScope Scope, ScopeSource Source) ScopeFor(InstallScope? explicitScope, bool keepsInstalled,
        InstallScope? msiContext, string? winGetScope, InstallScope setting, ScopeSource explicitSource = ScopeSource.Installed) =>
        explicitScope is InstallScope.User or InstallScope.System ? (explicitScope.Value, explicitSource)
        : !keepsInstalled ? (setting, ScopeSource.Setting)
        : msiContext is InstallScope.User or InstallScope.System ? (msiContext.Value, ScopeSource.Installed)
        : FromWinGetScope(winGetScope) is { } fromWinGet ? (fromWinGet, ScopeSource.Installed)
        : (setting, ScopeSource.Setting);

    /// <summary>WinGet's <c>InstalledScope</c> metadata: "User" or "Machine"; anything else is unknown.</summary>
    internal static InstallScope? FromWinGetScope(string? value) => InstalledScopes.FromWinGetScope(value);

    /// <summary>
    /// 003 R8.1, R8.2: WinGet's Default mode (msiexec <c>/passive</c>) when the installed version is an MSI product that
    /// Windows Installer has for all users: with <c>/quiet</c>, removing it fails with 1730 (admin needed) and no prompt.
    /// Updates (Pandoc's uninstallPrevious starts with that removal), uninstalls and going back. Everything else Silent.
    /// </summary>
    internal static OperationMode ModeFor(OperationKind kind, bool installed, InstallScope? msiContext) =>
        installed && msiContext == InstallScope.System && kind is OperationKind.Upgrade or OperationKind.Uninstall or OperationKind.Install
            ? OperationMode.DefaultForAllUsersMsi
            : OperationMode.Silent;

    private async Task<OperationOutcome> UninstallAsync(CatalogPackage package, string name, string? logPath, IProgress<OperationProgress> progress, CancellationToken cancellationToken)
    {
        var installed = package.InstalledVersion;
        var msiContext = installed is null ? null : await Task.Run(() => InstalledScopes.MsiContextOf(installed, _installContext), cancellationToken);
        var mode = ModeFor(OperationKind.Uninstall, installed is not null, msiContext);
        var options = new UninstallOptions
        {
            PackageUninstallMode = mode == OperationMode.DefaultForAllUsersMsi ? PackageUninstallMode.Default : PackageUninstallMode.Silent, // 003 R8.1
        };
        if (logPath is not null)
        {
            options.LogOutputPath = logPath;
        }
        if (mode == OperationMode.DefaultForAllUsersMsi)
        {
            progress.Report(new OperationProgress(OperationPhase.Queued) { MayAskPermission = true }); // 003 R8.3
        }
        IAsyncOperationWithProgress<UninstallResult, UninstallProgress> operation = connection.Manager.UninstallPackageAsync(package, options);
        var winGetProgress = new Progress<UninstallProgress>(p => progress.Report(MapUninstallProgress(p)));
        var result = await operation.AsTask(cancellationToken, winGetProgress);

        var extendedError = result.ExtendedErrorCode?.HResult ?? 0;
        var running = await RunningFilesAfterAsync(extendedError, package);
        return MapUninstallResult(result.Status, result.UninstallerErrorCode, result.RebootRequired, extendedError, name, running,
                installedVersion: installed?.Version, mode: mode)
            with { Details = FormatDetails(result.Status.ToString(), result.UninstallerErrorCode, result.ExtendedErrorCode), Mode = mode };
    }

    /// <summary>
    /// 003 R7.2: after a portable failure only, the programs running from the package's install folder. Nothing is read
    /// on success or on any other failure.
    /// </summary>
    private Task<IReadOnlyList<string>?> RunningFilesAfterAsync(int extendedError, CatalogPackage package) =>
        IsPortableFailure(extendedError)
            ? Task.Run(() => RunningFilesAfter(extendedError, () => package.InstalledVersion?.GetMetadata(PackageVersionMetadataField.InstalledLocation), runningPrograms))
            : Task.FromResult<IReadOnlyList<string>?>(null);

    /// <summary>
    /// The running files for R7.2, or null for R7.3: not a portable failure, no folder, or any error reading it or the
    /// process list. A failed check never becomes an error of its own.
    /// </summary>
    internal static IReadOnlyList<string>? RunningFilesAfter(int extendedError, Func<string?> installFolder, IRunningPrograms programs)
    {
        if (!IsPortableFailure(extendedError))
        {
            return null;
        }
        try
        {
            return installFolder() is { Length: > 0 } folder ? programs.FileNamesIn(folder) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // 004 R6.1: WinGet reports Ok while Windows only staged the new App Installer, because WinGet's own server, which this
    // app uses, still runs from the old one. WinGet's installed list then still has the old entry (the ID holds the version).
    private async Task<bool?> OldVersionStillInstalledAsync(PackageInfo package)
    {
        try
        {
            await connection.FindInstalledEntryAsync(package.InstalledId!, CancellationToken.None);
            return true;
        }
        catch (WinGetException)
        {
            return false; // not found: the new version is in
        }
        catch (Exception)
        {
            return null;
        }
    }

    internal enum CheckAfterOperation
    {
        None,
        AppInstaller,
        InstalledVersion,
    }

    /// <summary>Which re-read a finished operation gets: App Installer's own (004 R6.1, 013 R2.4), the version (013 R2.1), or none (R2.5).</summary>
    /// A chosen version installed over an installed package gets the version check too (014 R2.5).
    internal static CheckAfterOperation CheckAfter(OperationKind kind, OperationOutcome outcome, PackageInfo package, string? version = null) =>
        outcome.Result != OperationResult.Succeeded ? CheckAfterOperation.None
        : kind == OperationKind.Upgrade ? (WinGetHost.IsAppInstaller(package) ? CheckAfterOperation.AppInstaller : CheckAfterOperation.InstalledVersion)
        : GoesBack(kind, package, version) ? CheckAfterOperation.InstalledVersion
        : CheckAfterOperation.None;

    /// <summary>
    /// 014: an install of a chosen version over an installed package. The details pane only offers that for an older
    /// version, or one WinGet can't compare (R1.1, R1.2); a newer one is an upgrade.
    /// </summary>
    internal static bool GoesBack(OperationKind kind, PackageInfo package, string? version) =>
        kind == OperationKind.Install && version is not null && package.IsInstalled;

    /// <summary>The source's ID for a version, by index (W2); null when the source doesn't list it (014 R2.1).</summary>
    private static PackageVersionId? FindVersion(CatalogPackage package, string version)
    {
        var ids = package.AvailableVersions;
        for (var i = 0; i < ids.Count; i++)
        {
            if (string.Equals(ids[i].Version, version, StringComparison.Ordinal))
            {
                return ids[i];
            }
        }
        return null;
    }

    /// <summary>014 R2.1: the chosen version left the source since the pane opened. Retry would fail the same way.</summary>
    internal static OperationOutcome VersionUnavailable(string source) =>
        Failed($"This version is no longer available from {(string.IsNullOrEmpty(source) ? "the source" : source)}.") with { Retryable = false };

    /// <summary>
    /// 014 R3.3: an installer error while going back. T1 measured that installers refuse with their generic failure codes,
    /// so this says "may", and keeps Retry.
    /// </summary>
    internal static string GoingBackFailedMessage(string packageName, string version)
    {
        var name = string.IsNullOrWhiteSpace(packageName) ? "The package" : packageName;
        return $"{name} couldn't go back to {version}. Its installer may not install over a newer version. "
            + $"To go back, uninstall {name}, then install {version}. Uninstall, then install does both.";
    }

    /// <summary>The package as WinGet lists it after an update (013 R2.1).</summary>
    internal sealed record InstalledAfterUpdate(string? Version, bool UpdateAvailable);

    /// <summary>013 R2.1: reads the package again. Null when it can't be read: a failed check is never an error of its own (R2.3).</summary>
    private async Task<InstalledAfterUpdate?> InstalledAfterUpdateAsync(PackageInfo package)
    {
        try
        {
            var found = await connection.FindByIdAsync(package.Id, package.Source, localFirst: true, CancellationToken.None);
            return new InstalledAfterUpdate(found.InstalledVersion?.Version, found.IsUpdateAvailable);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// 013 R2.2, R2.3: a warning when WinGet said Ok but still lists the old version with an update; otherwise the outcome
    /// unchanged. Comparing with the old version, not the new one: installers often list a different version than the
    /// manifest's, so "not the new version" would warn after good updates.
    /// </summary>
    internal static OperationOutcome AfterUpdate(OperationOutcome outcome, string? versionBefore, InstalledAfterUpdate? after, string packageName = "")
    {
        if (after is not { Version: { } version, UpdateAvailable: true } || versionBefore is null
            || !string.Equals(version, versionBefore, StringComparison.Ordinal))
        {
            return outcome;
        }
        var name = string.IsNullOrWhiteSpace(packageName) ? "the package" : packageName;
        return outcome with
        {
            Result = OperationResult.SucceededUnconfirmed,
            Message = $"WinGet finished without an error, but Windows still lists {name} {version}. "
                + $"The installer may still be finishing, or {name} may update itself the next time it starts.",
        };
    }

    /// <summary>The outcome of an App Installer update once WinGet's installed list was read again (004 R6.1).</summary>
    /// <param name="oldVersionStillInstalled">Null when the list couldn't be read: report what WinGet said.</param>
    internal static OperationOutcome AfterAppInstallerUpdate(OperationOutcome outcome, bool? oldVersionStillInstalled) =>
        oldVersionStillInstalled == true
            ? outcome with
            {
                Result = OperationResult.SucceededAppRestartRequired,
                Message = "Updated. Restart CartonStash to finish: WinGet runs from App Installer, so the new version starts with it.",
            }
            : outcome;

    /// <summary>
    /// Uninstall removes the row's own program, found by its installed entry ID, never another program WinGet merged
    /// under the same package ID (002 R6.5). Install and upgrade still go through the WinGet ID.
    /// </summary>
    internal static bool UninstallsInstalledEntry(OperationKind kind, PackageInfo package) =>
        kind == OperationKind.Uninstall && !string.IsNullOrEmpty(package.InstalledId);

    internal static PackageInstallScope ToWinGet(InstallScope scope) => scope switch
    {
        InstallScope.User => PackageInstallScope.User,
        InstallScope.System => PackageInstallScope.System,
        _ => PackageInstallScope.Any,
    };

    internal static OperationProgress MapInstallProgress(InstallProgress p) => p.State switch
    {
        PackageInstallProgressState.Queued => new(OperationPhase.Queued),
        PackageInstallProgressState.Downloading => new(OperationPhase.Downloading, p.BytesRequired > 0 ? p.DownloadProgress * 100 : null),
        PackageInstallProgressState.Installing => new(OperationPhase.Running),
        _ => new(OperationPhase.Finishing),
    };

    internal static OperationProgress MapUninstallProgress(UninstallProgress p) => p.State switch
    {
        PackageUninstallProgressState.Queued => new(OperationPhase.Queued),
        PackageUninstallProgressState.Uninstalling => new(OperationPhase.Running),
        _ => new(OperationPhase.Finishing),
    };

    /// <summary>APPINSTALLER_CLI_ERROR_INSTALLER_HASH_MISMATCH: the download isn't the file the manifest describes.</summary>
    internal const int HashMismatch = unchecked((int)0x8A150011);

    /// <summary>APPINSTALLER_CLI_ERROR_INSTALL_PACKAGE_IN_USE: the program is running (003 R7.1).</summary>
    internal const int PackageInUse = unchecked((int)0x8A150101);

    /// <summary>APPINSTALLER_CLI_ERROR_INSTALL_FILE_IN_USE: one of its files is in use (003 R7.1).</summary>
    internal const int FileInUse = unchecked((int)0x8A150103);

    /// <summary>APPINSTALLER_CLI_ERROR_INSTALL_PACKAGE_IN_USE_BY_APPLICATION: another program uses it (003 R7.1).</summary>
    internal const int InUseByApplication = unchecked((int)0x8A150111);

    /// <summary>APPINSTALLER_CLI_ERROR_PORTABLE_INSTALL_FAILED: seen when a running claude.exe couldn't be replaced (003 R7.2).</summary>
    internal const int PortableInstallFailed = unchecked((int)0x8A150052);

    /// <summary>APPINSTALLER_CLI_ERROR_PORTABLE_UNINSTALL_FAILED (003 R7.2).</summary>
    internal const int PortableUninstallFailed = unchecked((int)0x8A150057);

    /// <summary>True for the codes after which the app looks for programs running from the package's folder (R7.2).</summary>
    internal static bool IsPortableFailure(int extendedError) => extendedError is PortableInstallFailed or PortableUninstallFailed;

    /// <summary>
    /// 003 R7.1-R7.3: the message when the package is running, or null to keep the usual one. A portable failure says so only
    /// when a running file was found; the app doesn't guess (constitution P1).
    /// </summary>
    internal static string? InUseMessage(OperationKind kind, int extendedError, string packageName, IReadOnlyList<string>? runningFiles)
    {
        var verb = kind switch
        {
            OperationKind.Upgrade => "updated",
            OperationKind.Uninstall => "uninstalled",
            _ => "installed",
        };
        var name = string.IsNullOrWhiteSpace(packageName) ? "The package" : packageName;
        return extendedError switch
        {
            PackageInUse or FileInUse => $"{name} is running, so it couldn't be {verb}. Close it, then press Retry.",
            InUseByApplication => $"{name} is in use by another program, so it couldn't be {verb}. Close that program, then press Retry.",
            _ when IsPortableFailure(extendedError) && runningFiles is { Count: > 0 } =>
                $"{name} is running ({string.Join(", ", runningFiles)}), so it couldn't be {verb}. Close it, then press Retry.",
            _ => null,
        };
    }

    /// <summary>APPINSTALLER_CLI_ERROR_EXEC_UNINSTALL_COMMAND_FAILED: WinGet's uninstall command failed (003 R8.4).</summary>
    internal const int UninstallCommandFailed = unchecked((int)0x8A150030);

    /// <summary>
    /// 003 R8.4: WinGet couldn't uninstall the installed version, for an uninstall or at the start of an update (an
    /// uninstallPrevious manifest). After a Silent run with 1603 or 1730 (Windows Installer: "You must be an
    /// Administrator"), the likely cause is an all-users install the app didn't recognise, so say so, with "may".
    /// </summary>
    internal static string UninstallCommandFailedMessage(string packageName, string? installedVersion, uint exitCode, OperationMode mode)
    {
        var name = string.IsNullOrWhiteSpace(packageName) ? "the package" : packageName;
        var what = installedVersion is null ? name : $"{name} {installedVersion}";
        var text = $"WinGet couldn't uninstall {what} (exit code {FormatCode(exitCode)}).";
        if (mode == OperationMode.Silent && exitCode is 1603 or 1730)
        {
            var subject = string.IsNullOrWhiteSpace(packageName) ? "It" : packageName;
            text += $" {subject} may be installed for all users, which needs administrator rights. Retry, and accept Windows' prompt if it appears.";
        }
        return text;
    }

    /// <summary>APPINSTALLER_CLI_ERROR_INSTALL_UPGRADE_NOT_SUPPORTED: the manifest denies an in-place update (013 R3.1).</summary>
    internal const int UpgradeNotSupported = unchecked((int)0x8A150114);

    /// <summary>013 R3.1: what to do instead. The app doesn't do it itself (R3.2, constitution P2).</summary>
    internal static string UpgradeNotSupportedMessage(string packageName)
    {
        var name = string.IsNullOrWhiteSpace(packageName) ? "This package" : packageName;
        return $"{name} can't be updated in place: its publisher asks for it to be uninstalled first. "
            + "To get the new version, uninstall it, then install it again.";
    }

    /// <summary>CO_E_APPNOTFOUND: WinGet couldn't start a program, seen for the EA app's missing uninstaller (013 R1.2).</summary>
    internal const int AppNotFound = unchecked((int)0x800401F5);

    /// <summary>HRESULT_FROM_WIN32(ERROR_FILE_NOT_FOUND) (013 R1.2).</summary>
    internal const int FileNotFound = unchecked((int)0x80070002);

    /// <summary>
    /// 013 R1.1, R1.2: the message when WinGet failed with an error but no installer exit code. It doesn't say the installer
    /// never ran: that's likely, but WinGet can also fail after it (constitution P1).
    /// </summary>
    internal static string NoExitCodeMessage(OperationKind kind, int extendedError, string packageName, Func<int, string?> describe)
    {
        var name = string.IsNullOrWhiteSpace(packageName) ? "the package" : packageName;
        var code = $"0x{extendedError:X8}";
        if (extendedError is AppNotFound or FileNotFound)
        {
            var purpose = kind switch
            {
                OperationKind.Upgrade => "update",
                OperationKind.Uninstall => "uninstall",
                _ => "install",
            };
            return $"WinGet couldn't start a program needed to {purpose} {name}: it wasn't found ({code}). "
                + $"Repairing or reinstalling {name} from its publisher may fix this.";
        }
        var noun = kind switch
        {
            OperationKind.Upgrade => "The update",
            OperationKind.Uninstall => "The uninstall",
            _ => "The install",
        };
        string? text;
        try
        {
            text = describe(extendedError);
        }
        catch (Exception)
        {
            text = null; // a description is a bonus, never an error of its own
        }
        return string.IsNullOrWhiteSpace(text)
            ? $"{noun} failed: WinGet reported error {code}."
            : $"{noun} failed: WinGet reported \"{text}\" ({code}).";
    }

    // Readable outcomes (003 R5.2, R5.3, R7; 004 NoApplicableUpgrade; 006 scope hint).
    /// <param name="goingBackTo">The chosen older version when this install goes back over a newer one (014 R3.3), else null.</param>
    internal static OperationOutcome MapInstallResult(OperationKind kind, InstallResultStatus status, uint installerErrorCode, bool rebootRequired,
        InstallScope scope, int extendedError = 0, string packageName = "", IReadOnlyList<string>? runningFiles = null,
        Func<int, string?>? describe = null, string? goingBackTo = null, string? installedVersion = null, OperationMode mode = OperationMode.Silent)
    {
        var done = kind == OperationKind.Upgrade ? "Updated" : "Installed";
        return status switch
        {
            // 013 R3.1: whatever the status (WinGet reports CatalogError), the code says the update isn't allowed in place.
            not InstallResultStatus.Ok when kind == OperationKind.Upgrade && extendedError == UpgradeNotSupported =>
                Failed(UpgradeNotSupportedMessage(packageName)) with { Retryable = false },
            InstallResultStatus.Ok when rebootRequired =>
                new(OperationResult.SucceededRestartRequired, $"{done}. Restart your PC to finish."),
            InstallResultStatus.Ok => new(OperationResult.Succeeded, $"{done} successfully."),
            InstallResultStatus.DownloadError when extendedError == HashMismatch =>
                Failed("The downloaded file doesn't match the package's checksum, so it wasn't installed. The publisher's site may have changed; try again later."),
            InstallResultStatus.DownloadError => Failed("The download failed. Check your internet connection and try again."),
            InstallResultStatus.InstallError when InUseMessage(kind, extendedError, packageName, runningFiles) is { } inUse => Failed(inUse),
            // 003 R8.4: WinGet's removal of the installed version failed, not the new version's installer.
            InstallResultStatus.InstallError when extendedError == UninstallCommandFailed =>
                Failed(UninstallCommandFailedMessage(packageName, installedVersion, installerErrorCode, mode)),
            InstallResultStatus.InstallError when goingBackTo is not null =>
                Failed(GoingBackFailedMessage(packageName, goingBackTo)) with { OffersUninstallFirst = true }, // R3.5
            InstallResultStatus.InstallError when installerErrorCode == 0 && extendedError != 0 =>
                Failed(NoExitCodeMessage(kind, extendedError, packageName, describe ?? ErrorDescriptions.Describe)),
            InstallResultStatus.InstallError => Failed($"The installer failed with error code {FormatCode(installerErrorCode)}."),
            InstallResultStatus.BlockedByPolicy => Failed("Blocked by a group policy on this PC."),
            InstallResultStatus.NoApplicableInstallers => Failed(scope == InstallScope.Any
                ? "No installer for this PC (architecture or OS version)."
                : "No installer for this PC with the selected install scope. Try changing it in Settings."),
            InstallResultStatus.NoApplicableUpgrade => Failed("No applicable update for this PC."),
            InstallResultStatus.PackageAgreementsNotAccepted => Failed("The package's license agreement wasn't accepted."),
            InstallResultStatus.CatalogError => Failed("Couldn't reach the package source. Check your internet connection."),
            InstallResultStatus.ManifestError => Failed("The package's manifest is invalid. Try again later."),
            _ => Failed($"WinGet reported {status}."),
        };
    }

    internal static OperationOutcome MapUninstallResult(UninstallResultStatus status, uint uninstallerErrorCode, bool rebootRequired,
        int extendedError = 0, string packageName = "", IReadOnlyList<string>? runningFiles = null, Func<int, string?>? describe = null,
        string? installedVersion = null, OperationMode mode = OperationMode.Silent) => status switch
    {
        UninstallResultStatus.Ok when rebootRequired =>
            new(OperationResult.SucceededRestartRequired, "Uninstalled. Restart your PC to finish."),
        UninstallResultStatus.Ok => new(OperationResult.Succeeded, "Uninstalled successfully."),
        UninstallResultStatus.UninstallError when InUseMessage(OperationKind.Uninstall, extendedError, packageName, runningFiles) is { } inUse =>
            Failed(inUse),
        UninstallResultStatus.UninstallError when extendedError == UninstallCommandFailed => // 003 R8.4
            Failed(UninstallCommandFailedMessage(packageName, installedVersion, uninstallerErrorCode, mode)),
        UninstallResultStatus.UninstallError when uninstallerErrorCode == 0 && extendedError != 0 =>
            Failed(NoExitCodeMessage(OperationKind.Uninstall, extendedError, packageName, describe ?? ErrorDescriptions.Describe)),
        UninstallResultStatus.UninstallError => Failed($"The uninstaller failed with error code {FormatCode(uninstallerErrorCode)}."),
        UninstallResultStatus.BlockedByPolicy => Failed("Blocked by a group policy on this PC."),
        UninstallResultStatus.CatalogError => Failed("Couldn't read the installed packages."),
        _ => Failed($"WinGet reported {status}."),
    };

    /// <summary>The technical result lines of the log (009 R2.2).</summary>
    internal static string FormatDetails(string status, uint installerExitCode, Exception? extendedError) =>
        $"WinGet status: {status}\nInstaller exit code: {installerExitCode}\nExtended error: "
        + (extendedError is null ? "none" : $"0x{extendedError.HResult:X8}");

    private static OperationOutcome Failed(string message) => new(OperationResult.Failed, message);

    // MSI / Windows codes read better in hex once they're above the small range.
    private static string FormatCode(uint code) => code > 0xFFFF ? $"0x{code:X8}" : code.ToString();
}
