using WinGetFrontend.Core.Models;
using WinGetFrontend.Core.Services;

namespace WinGetFrontend.Core.ViewModels;

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
public sealed class PackageActions(PackageOperationQueue queue, IPackageInstaller installer, IDialogService dialogs)
{
    public PackageOperationQueue Queue => queue;

    /// <summary>Install, after the user accepts the package's agreements if it has any (003 R1.2, R1.3).</summary>
    public async Task InstallAsync(PackageInfo package)
    {
        if (queue.Find(package.Id) is { IsActive: true })
        {
            return; // R4.2
        }

        IReadOnlyList<Agreement> agreements;
        try
        {
            agreements = await installer.GetAgreementsAsync(package);
        }
        catch (Exception)
        {
            // Can't read them: install without accepting. WinGet refuses with PackageAgreementsNotAccepted
            // if there were any, and the Activity entry says so.
            agreements = [];
        }

        var accepted = false;
        if (agreements.Count > 0)
        {
            if (!await dialogs.ShowAgreementsAsync($"{package.Name}: license agreement", agreements))
            {
                return;
            }
            accepted = true;
        }

        queue.Enqueue(OperationKind.Install, package, accepted);
    }

    /// <summary>Uninstall, only after the user confirms (003 R2.2, constitution P2).</summary>
    public async Task UninstallAsync(PackageInfo package)
    {
        if (queue.Find(package.Id) is { IsActive: true })
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

    /// <summary>Update one package (004 R2.1).</summary>
    public void Update(PackageInfo package) => queue.Enqueue(OperationKind.Upgrade, package);

    /// <summary>Update several. With <paramref name="confirm"/>, asks first, naming the count (004 R2.3).</summary>
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

        queue.EnqueueMany(OperationKind.Upgrade, packages);
        return true;
    }
}
