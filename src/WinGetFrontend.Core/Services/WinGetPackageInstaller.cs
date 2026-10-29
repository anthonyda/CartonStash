using Microsoft.Management.Deployment;
using Windows.Foundation;
using WinGetFrontend.Core.Models;

namespace WinGetFrontend.Core.Services;

/// <summary>
/// <see cref="IPackageInstaller"/> on the WinGet COM API (specs 003, 004). WinGet starts the installer, and the
/// installer asks for UAC itself when it needs to; the app never elevates (003 R6.1, constitution A5).
/// </summary>
public sealed class WinGetPackageInstaller(CatalogConnection connection, ISettings settings) : IPackageInstaller
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

    public async Task<OperationOutcome> RunAsync(OperationKind kind, PackageInfo package, bool acceptAgreements,
        IProgress<OperationProgress> progress, CancellationToken cancellationToken)
    {
        // Re-find the package by ID; COM objects never leave this class (A2).
        var found = await connection.FindByIdAsync(package.Id, package.Source, localFirst: kind != OperationKind.Install, cancellationToken);
        try
        {
            return kind switch
            {
                OperationKind.Uninstall => await UninstallAsync(found, progress, cancellationToken),
                _ => await InstallAsync(kind, found, acceptAgreements, progress, cancellationToken),
            };
        }
        finally
        {
            // The system changed (or may have): next reads must not use stale catalogs (002 R1.3).
            connection.Invalidate();
        }
    }

    private async Task<OperationOutcome> InstallAsync(OperationKind kind, CatalogPackage package, bool acceptAgreements,
        IProgress<OperationProgress> progress, CancellationToken cancellationToken)
    {
        var options = new InstallOptions
        {
            PackageInstallMode = PackageInstallMode.Silent,
            PackageInstallScope = ToWinGet(settings.Current.InstallScope), // 006 R2.1
            AcceptPackageAgreements = acceptAgreements,
        };

        var operation = kind == OperationKind.Upgrade
            ? connection.Manager.UpgradePackageAsync(package, options)
            : connection.Manager.InstallPackageAsync(package, options);
        var winGetProgress = new Progress<InstallProgress>(p => progress.Report(MapInstallProgress(p)));
        var result = await operation.AsTask(cancellationToken, winGetProgress);

        return MapInstallResult(kind, result.Status, result.InstallerErrorCode, result.RebootRequired, settings.Current.InstallScope,
            result.ExtendedErrorCode?.HResult ?? 0);
    }

    private async Task<OperationOutcome> UninstallAsync(CatalogPackage package, IProgress<OperationProgress> progress, CancellationToken cancellationToken)
    {
        var options = new UninstallOptions { PackageUninstallMode = PackageUninstallMode.Silent };
        IAsyncOperationWithProgress<UninstallResult, UninstallProgress> operation = connection.Manager.UninstallPackageAsync(package, options);
        var winGetProgress = new Progress<UninstallProgress>(p => progress.Report(MapUninstallProgress(p)));
        var result = await operation.AsTask(cancellationToken, winGetProgress);

        return MapUninstallResult(result.Status, result.UninstallerErrorCode, result.RebootRequired);
    }

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

    // Readable outcomes (003 R5.2, R5.3; 004 NoApplicableUpgrade; 006 scope hint).
    internal static OperationOutcome MapInstallResult(OperationKind kind, InstallResultStatus status, uint installerErrorCode, bool rebootRequired,
        InstallScope scope, int extendedError = 0)
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

    internal static OperationOutcome MapUninstallResult(UninstallResultStatus status, uint uninstallerErrorCode, bool rebootRequired) => status switch
    {
        UninstallResultStatus.Ok when rebootRequired =>
            new(OperationResult.SucceededRestartRequired, "Uninstalled. Restart your PC to finish."),
        UninstallResultStatus.Ok => new(OperationResult.Succeeded, "Uninstalled successfully."),
        UninstallResultStatus.UninstallError => Failed($"The uninstaller failed with error code {FormatCode(uninstallerErrorCode)}."),
        UninstallResultStatus.BlockedByPolicy => Failed("Blocked by a group policy on this PC."),
        UninstallResultStatus.CatalogError => Failed("Couldn't read the installed packages."),
        _ => Failed($"WinGet reported {status}."),
    };

    private static OperationOutcome Failed(string message) => new(OperationResult.Failed, message);

    // MSI / Windows codes read better in hex once they're above the small range.
    private static string FormatCode(uint code) => code > 0xFFFF ? $"0x{code:X8}" : code.ToString();
}
