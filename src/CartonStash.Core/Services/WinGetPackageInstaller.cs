using Microsoft.Management.Deployment;
using Windows.Foundation;
using CartonStash.Core.Models;

namespace CartonStash.Core.Services;

/// <summary>
/// <see cref="IPackageInstaller"/> on the WinGet COM API (specs 003, 004). WinGet starts the installer, and the
/// installer asks for UAC itself when it needs to; the app never elevates (003 R6.1, constitution A5).
/// </summary>
public sealed class WinGetPackageInstaller(CatalogConnection connection, ISettings settings, IRunningPrograms runningPrograms) : IPackageInstaller
{
    public async Task<IReadOnlyList<Agreement>> GetAgreementsAsync(PackageInfo package, CancellationToken cancellationToken = default)
    {
        var found = await connection.FindByIdAsync(package.Id, package.Source, localFirst: false, cancellationToken);
        var version = found.DefaultInstallVersion;
        if (version is null)
        {
            return [];
        }

        return await Task.Run<IReadOnlyList<Agreement>>(() =>
        {
            var agreements = version.GetCatalogPackageMetadata().Agreements;
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

    public async Task<OperationOutcome> RunAsync(OperationKind kind, PackageInfo package, bool acceptAgreements, string? logPath,
        IProgress<OperationProgress> progress, CancellationToken cancellationToken)
    {
        // Re-find the package; COM objects never leave this class (A2).
        var found = UninstallsInstalledEntry(kind, package)
            ? await connection.FindInstalledEntryAsync(package.InstalledId!, cancellationToken)
            : await connection.FindByIdAsync(package.Id, package.Source, localFirst: kind != OperationKind.Install, cancellationToken);
        try
        {
            var outcome = kind switch
            {
                OperationKind.Uninstall => await UninstallAsync(found, package.Name, logPath, progress, cancellationToken),
                _ => await InstallAsync(kind, found, package.Name, acceptAgreements, logPath, progress, cancellationToken),
            };
            if (kind == OperationKind.Upgrade && outcome.Result == OperationResult.Succeeded && WinGetHost.IsAppInstaller(package))
            {
                connection.Invalidate();
                outcome = AfterAppInstallerUpdate(outcome, await OldVersionStillInstalledAsync(package));
            }
            return outcome;
        }
        finally
        {
            // The system changed (or may have): next reads must not use stale catalogs (002 R1.3).
            connection.Invalidate();
        }
    }

    private async Task<OperationOutcome> InstallAsync(OperationKind kind, CatalogPackage package, string name, bool acceptAgreements, string? logPath,
        IProgress<OperationProgress> progress, CancellationToken cancellationToken)
    {
        var options = new InstallOptions
        {
            PackageInstallMode = PackageInstallMode.Silent,
            PackageInstallScope = ToWinGet(settings.Current.InstallScope), // 006 R2.1
            AcceptPackageAgreements = acceptAgreements,
        };
        if (logPath is not null)
        {
            options.LogOutputPath = logPath;
        }

        var operation = kind == OperationKind.Upgrade
            ? connection.Manager.UpgradePackageAsync(package, options)
            : connection.Manager.InstallPackageAsync(package, options);
        var winGetProgress = new Progress<InstallProgress>(p => progress.Report(MapInstallProgress(p)));
        var result = await operation.AsTask(cancellationToken, winGetProgress);

        var extendedError = result.ExtendedErrorCode?.HResult ?? 0;
        var running = await RunningFilesAfterAsync(extendedError, package);
        return MapInstallResult(kind, result.Status, result.InstallerErrorCode, result.RebootRequired, settings.Current.InstallScope,
            extendedError, name, running) with { Details = FormatDetails(result.Status.ToString(), result.InstallerErrorCode, result.ExtendedErrorCode) };
    }

    private async Task<OperationOutcome> UninstallAsync(CatalogPackage package, string name, string? logPath, IProgress<OperationProgress> progress, CancellationToken cancellationToken)
    {
        var options = new UninstallOptions { PackageUninstallMode = PackageUninstallMode.Silent };
        if (logPath is not null)
        {
            options.LogOutputPath = logPath;
        }
        IAsyncOperationWithProgress<UninstallResult, UninstallProgress> operation = connection.Manager.UninstallPackageAsync(package, options);
        var winGetProgress = new Progress<UninstallProgress>(p => progress.Report(MapUninstallProgress(p)));
        var result = await operation.AsTask(cancellationToken, winGetProgress);

        var extendedError = result.ExtendedErrorCode?.HResult ?? 0;
        var running = await RunningFilesAfterAsync(extendedError, package);
        return MapUninstallResult(result.Status, result.UninstallerErrorCode, result.RebootRequired, extendedError, name, running)
            with { Details = FormatDetails(result.Status.ToString(), result.UninstallerErrorCode, result.ExtendedErrorCode) };
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

    // Readable outcomes (003 R5.2, R5.3, R7; 004 NoApplicableUpgrade; 006 scope hint).
    internal static OperationOutcome MapInstallResult(OperationKind kind, InstallResultStatus status, uint installerErrorCode, bool rebootRequired,
        InstallScope scope, int extendedError = 0, string packageName = "", IReadOnlyList<string>? runningFiles = null)
    {
        var done = kind == OperationKind.Upgrade ? "Updated" : "Installed";
        return status switch
        {
            InstallResultStatus.Ok when rebootRequired =>
                new(OperationResult.SucceededRestartRequired, $"{done}. Restart your PC to finish."),
            InstallResultStatus.Ok => new(OperationResult.Succeeded, $"{done} successfully."),
            InstallResultStatus.DownloadError when extendedError == HashMismatch =>
                Failed("The downloaded file doesn't match the package's checksum, so it wasn't installed. The publisher's site may have changed; try again later."),
            InstallResultStatus.DownloadError => Failed("The download failed. Check your internet connection and try again."),
            InstallResultStatus.InstallError when InUseMessage(kind, extendedError, packageName, runningFiles) is { } inUse => Failed(inUse),
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
        int extendedError = 0, string packageName = "", IReadOnlyList<string>? runningFiles = null) => status switch
    {
        UninstallResultStatus.Ok when rebootRequired =>
            new(OperationResult.SucceededRestartRequired, "Uninstalled. Restart your PC to finish."),
        UninstallResultStatus.Ok => new(OperationResult.Succeeded, "Uninstalled successfully."),
        UninstallResultStatus.UninstallError when InUseMessage(OperationKind.Uninstall, extendedError, packageName, runningFiles) is { } inUse =>
            Failed(inUse),
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
