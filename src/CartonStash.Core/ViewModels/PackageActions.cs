using CartonStash.Core.Models;
using CartonStash.Core.Services;

namespace CartonStash.Core.ViewModels;

/// <summary>Which action buttons a list shows on its rows.</summary>
[Flags]
public enum RowActions
{
    None = 0,
    Install = 1,
    Uninstall = 2,
    Update = 4,
    All = Install | Uninstall | Update,
}

/// <summary>
/// The user-facing steps around each operation (confirmations, agreements), shared by rows, the details pane
/// and the Updates page. The queue does the actual work.
/// </summary>
public sealed class PackageActions(PackageOperationQueue queue, IPackageInstaller installer, IDialogService dialogs, IPackageService packages,
    WinGetHost? host = null)
{
    public PackageOperationQueue Queue => queue;

    /// <summary>App Installer's update waiting for a restart, for rows (004 R6.2).</summary>
    public WinGetHost Host { get; } = Track(queue, host ?? new WinGetHost());

    // 004 R6.1: the version stays in use until CartonStash restarts.
    private static WinGetHost Track(PackageOperationQueue queue, WinGetHost host)
    {
        queue.Completed += (_, operation) =>
        {
            if (operation.Result == OperationResult.SucceededAppRestartRequired && operation.Package.InstalledVersion is { } version)
            {
                host.SetPending(version);
            }
        };
        return host;
    }

    /// <summary>The package's icon, for rows (005 R5). Never throws: no icon is fine.</summary>
    public async Task<Uri?> GetIconAsync(PackageInfo package)
    {
        if (!package.IsManaged)
        {
            return null;
        }
        try
        {
            return PackageDetailsViewModel.SafeIconUri(await packages.GetIconUrlAsync(package));
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Install, after the user accepts the package's agreements if it has any (003 R1.2, R1.3).</summary>
    public async Task InstallAsync(PackageInfo package)
    {
        if (queue.Find(package) is { IsActive: true })
        {
            return; // R4.2
        }

        if (await AskAgreementsAsync(package) is { } accepted)
        {
            queue.Enqueue(OperationKind.Install, package, accepted);
        }
    }

    /// <summary>
    /// Shows the agreements of the version WinGet would install, if it has any (003 R1.3, 004 R7.1). Null when the user
    /// declined; otherwise whether WinGet may treat them as accepted.
    /// </summary>
    private async Task<bool?> AskAgreementsAsync(PackageInfo package)
    {
        IReadOnlyList<Agreement> agreements;
        try
        {
            agreements = await installer.GetAgreementsAsync(package);
        }
        catch (Exception)
        {
            // Can't read them: go ahead without accepting. WinGet refuses with PackageAgreementsNotAccepted
            // if there were any, and the Activity entry says so (004 R7.4).
            return false;
        }

        if (agreements.Count == 0)
        {
            return false;
        }
        return await dialogs.ShowAgreementsAsync($"{package.Name}: license agreement", agreements) ? true : null;
    }

    /// <summary>Uninstall, only after the user confirms (003 R2.2, constitution P2).</summary>
    public async Task UninstallAsync(PackageInfo package)
    {
        if (queue.Find(package) is { IsActive: true })
        {
            return;
        }

        var confirmed = await dialogs.ConfirmAsync(
            $"Uninstall {package.Name}?",
            $"{package.Name} ({package.Id}) will be removed from this PC.",
            "Uninstall");
        if (confirmed)
        {
            queue.Enqueue(OperationKind.Uninstall, package);
        }
    }

    /// <summary>Update one package, after the user accepts its agreements if it has any (004 R2.1, R7.1).</summary>
    public async Task UpdateAsync(PackageInfo package)
    {
        if (queue.Find(package) is { IsActive: true })
        {
            return; // 003 R4.2
        }

        if (await AskAgreementsAsync(package) is { } accepted)
        {
            queue.Enqueue(OperationKind.Upgrade, package, accepted);
        }
    }

    /// <summary>
    /// Update several. With <paramref name="confirm"/>, asks first, naming the count (004 R2.3). Then asks for the agreements
    /// of each package that has them, and queues nothing until all are answered; declined packages are left out (R7.2).
    /// </summary>
    public async Task<bool> UpdateManyAsync(IReadOnlyList<PackageInfo> packages, bool confirm)
    {
        if (packages.Count == 0)
        {
            return false;
        }

        if (confirm)
        {
            var noun = packages.Count == 1 ? "package" : "packages";
            var confirmed = await dialogs.ConfirmAsync(
                "Update all?",
                $"{packages.Count} {noun} will be updated. Installers run one after another and may show their own prompts.",
                $"Update {packages.Count} {noun}");
            if (!confirmed)
            {
                return false;
            }
        }

        var kept = new List<(PackageInfo Package, bool Accepted)>();
        foreach (var package in AppInstallerLast(packages))
        {
            if (queue.Find(package) is { IsActive: true })
            {
                continue; // 003 R4.2
            }
            if (await AskAgreementsAsync(package) is { } accepted)
            {
                kept.Add((package, accepted));
            }
        }
        foreach (var (package, accepted) in kept)
        {
            queue.Enqueue(OperationKind.Upgrade, package, accepted);
        }
        return kept.Count > 0;
    }

    /// <summary>
    /// 004 R6.5: App Installer's update only takes over when WinGet restarts, so the others run first, on the WinGet that
    /// started them, and the restart comes last. Otherwise the order is kept.
    /// </summary>
    internal static IEnumerable<PackageInfo> AppInstallerLast(IEnumerable<PackageInfo> packages) =>
        packages.OrderBy(WinGetHost.IsAppInstaller);
}
