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
                _ => await InstallAsync(kind, found, package, acceptAgreements, version, logPath, progress, cancellationToken),
            };
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
        string? logPath, IProgress<OperationProgress> progress, CancellationToken cancellationToken)
    {
        var name = info.Name;
        var options = new InstallOptions
        {
            PackageInstallMode = PackageInstallMode.Silent,
            PackageInstallScope = ToWinGet(settings.Current.InstallScope), // 006 R2.1
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
                return VersionUnavailable(info.Source);
            }
            options.PackageVersionId = id;
        }

        var operation = kind == OperationKind.Upgrade
            ? connection.Manager.UpgradePackageAsync(package, options)
            : connection.Manager.InstallPackageAsync(package, options);
        var winGetProgress = new Progress<InstallProgress>(p => progress.Report(MapInstallProgress(p)));
        var result = await operation.AsTask(cancellationToken, winGetProgress);

        var extendedError = result.ExtendedErrorCode?.HResult ?? 0;
        var running = await RunningFilesAfterAsync(extendedError, package);
        return MapInstallResult(kind, result.Status, result.InstallerErrorCode, result.RebootRequired, settings.Current.InstallScope,
            extendedError, name, running, goingBackTo: GoesBack(kind, info, version) ? version : null) with { Details = FormatDetails(result.Status.ToString(), result.InstallerErrorCode, result.ExtendedErrorCode) };
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
            + $"To go back, uninstall {name}, then install {version}.";
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
        Func<int, string?>? describe = null, string? goingBackTo = null)
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
            InstallResultStatus.InstallError when goingBackTo is not null => Failed(GoingBackFailedMessage(packageName, goingBackTo)),
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
        int extendedError = 0, string packageName = "", IReadOnlyList<string>? runningFiles = null, Func<int, string?>? describe = null) => status switch
    {
        UninstallResultStatus.Ok when rebootRequired =>
            new(OperationResult.SucceededRestartRequired, "Uninstalled. Restart your PC to finish."),
        UninstallResultStatus.Ok => new(OperationResult.Succeeded, "Uninstalled successfully."),
        UninstallResultStatus.UninstallError when InUseMessage(OperationKind.Uninstall, extendedError, packageName, runningFiles) is { } inUse =>
            Failed(inUse),
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
