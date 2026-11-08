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
    ISettings? settings = null, WinGetHost? host = null)
{
    public PackageOperationQueue Queue => queue;

    /// <summary>App Installer's update waiting for a restart, for rows (004 R6.2).</summary>
    public WinGetHost Host { get; } = Track(queue, host ?? new WinGetHost(), settings);

    // 004 R6.1: the version stays in use until CartonStash restarts. 014 R3.2: the exclusion the user asked for, once it worked.
    private static WinGetHost Track(PackageOperationQueue queue, WinGetHost host, ISettings? settings)
    {
        queue.Completed += (_, operation) =>
        {
            if (operation.Result == OperationResult.SucceededAppRestartRequired && operation.Package.InstalledVersion is { } version)
            {
                host.SetPending(version);
            }
            if (ExcludesAfter(operation))
            {
                settings?.Update(s => s.WithExcluded(operation.Package.Id));
            }
        };
        return host;
    }

    /// <summary>
    /// 014 R3.2: only a real success adds the exclusion. Not a warning (nothing changed, 013 R2.2), a failure or a cancel.
    /// </summary>
    internal static bool ExcludesAfter(PackageOperation operation) =>
        operation.Options.ExcludeOnSuccess && operation.Result is OperationResult.Succeeded or OperationResult.SucceededRestartRequired;

    /// <summary>
    /// 014: installs a version the user chose in the details pane. Going back (or a version WinGet can't compare) asks
    /// first, with the option to leave the package out of update checks (R3.1, R3.2); then the chosen version's
    /// agreements (R2.2). Nothing while the package is busy (003 R4.2).
    /// </summary>
    public async Task InstallVersionAsync(PackageInfo package, PackageVersion version)
    {
        if (queue.Find(package) is { IsActive: true } || version.Direction == VersionDirection.Installed)
        {
            return;
        }

        var options = new OperationOptions { Version = version.Version };
        if (version.Direction is VersionDirection.Older or VersionDirection.Unknown)
        {
            var (title, message, confirmText) = GoingBackQuestion(package, version);
            var alreadyExcluded = settings?.Current.IsExcluded(package.Id) ?? true;
            var (confirmed, exclude) = await dialogs.ConfirmWithOptionAsync(title, message, confirmText,
                alreadyExcluded ? null : $"Leave {package.Name} out of update checks", optionChecked: true);
            if (!confirmed)
            {
                return;
            }
            options = options with { ExcludeOnSuccess = exclude };
        }

        if (await AskAgreementsAsync(package, version.Version) is { } accepted)
        {
            var kind = version.Direction == VersionDirection.Newer ? OperationKind.Upgrade : OperationKind.Install;
            queue.Enqueue(kind, package, accepted, options);
        }
    }

    /// <summary>The going-back confirmation (014 R3.1): names the package and both versions.</summary>
    internal static (string Title, string Message, string ConfirmText) GoingBackQuestion(PackageInfo package, PackageVersion version)
    {
        var name = package.Name;
        var v = version.Version;
        var older = version.Direction == VersionDirection.Older;
        var doing = older ? $"Going back to {v}" : $"Installing {v}";
        return (
            older ? $"Go back to {name} {v}?" : $"Install {name} {v}?",
            $"{name} {package.InstalledVersion} is installed. {doing} runs that version's installer over it. "
                + $"Some installers can't go back, and {name}'s settings may not carry over to an older version.",
            older ? $"Go back to {v}" : $"Install {v}");
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
    private async Task<bool?> AskAgreementsAsync(PackageInfo package, string? version = null)
    {
        IReadOnlyList<Agreement> agreements;
        try
        {
            agreements = await installer.GetAgreementsAsync(package, version);
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
