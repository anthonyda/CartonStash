using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CartonStash.Core.Models;
using CartonStash.Core.Services;

namespace CartonStash.Core.ViewModels;

/// <summary>The color of the "Installed" badge (002 R7.1).</summary>
public enum VersionBadge
{
    /// <summary>Unmanaged, uncomparable, or different without an offered update.</summary>
    Neutral,
    Same,
    UpdateOffered,
    Newer,
}

/// <summary>
/// One row in Search / Installed / Updates (and the details pane header): the package plus the state of its
/// latest operation (003 R3.1). The owning page keeps <see cref="Operation"/> current.
/// </summary>
public sealed partial class PackageRowViewModel : ObservableObject
{
    private readonly PackageActions _actions;
    private readonly RowActions _allowed;
    private readonly ISettings? _exclusions;
    private Task? _iconLoad;

    /// <param name="exclusions">Given where a row can be excluded from update checks: the Updates page (010 R5.1).</param>
    public PackageRowViewModel(PackageInfo package, PackageActions actions, RowActions allowed, ISettings? exclusions = null)
    {
        Package = package;
        _actions = actions;
        _allowed = allowed;
        _exclusions = exclusions;
        Operation = actions.Queue.Find(package);
    }

    public PackageInfo Package { get; }

    public string Id => Package.Id;

    public string Name => Package.Name;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy), nameof(ShowInstall), nameof(ShowUpdate), nameof(ShowRestart), nameof(ShowUninstall), nameof(ResultText), nameof(HasResult))]
    public partial PackageOperation? Operation { get; set; }

    /// <summary>Badge color: WinGet's comparison, and amber only when WinGet also offers the update (R7.1, R7.2).</summary>
    public VersionBadge VersionBadge => !Package.IsManaged || Package.AvailableVersion is null
        ? VersionBadge.Neutral
        : Package.VersionStatus switch
        {
            VersionStatus.Same => VersionBadge.Same,
            VersionStatus.Older when Package.HasUpdate => VersionBadge.UpdateOffered,
            VersionStatus.Newer => VersionBadge.Newer,
            _ => VersionBadge.Neutral,
        };

    public string VersionBadgeText => $"Installed {Package.InstalledVersion}";

    /// <summary>The badge's meaning in words, for the tooltip and screen readers (R7.3).</summary>
    public string VersionStatusText => VersionBadge switch
    {
        VersionBadge.Same => $"Installed {Package.InstalledVersion}, same as {Package.Source}",
        VersionBadge.UpdateOffered => $"Installed {Package.InstalledVersion}, update to {Package.AvailableVersion} available from {Package.Source}",
        VersionBadge.Newer => $"Installed {Package.InstalledVersion}, newer than {Package.Source}'s {Package.AvailableVersion}",
        _ when Package.IsManaged && Package.AvailableVersion is not null =>
            $"Installed {Package.InstalledVersion}, {Package.Source} has {Package.AvailableVersion}",
        _ => $"Installed {Package.InstalledVersion}",
    };

    /// <summary>The package's icon from WinGet; null shows a generic glyph (005 R5.1).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasIcon))]
    public partial Uri? IconUri { get; private set; }

    public bool HasIcon => IconUri is not null;

    /// <summary>Loads the icon once. The row control calls this when it shows the row, so only rows on screen ask (R5.2).</summary>
    public Task EnsureIconAsync() => _iconLoad ??= LoadIconAsync();

    private async Task LoadIconAsync() => IconUri = await _actions.GetIconAsync(Package);

    /// <summary>An operation for this package is queued or running (R4.2: buttons give way to progress).</summary>
    public bool IsBusy => Operation?.IsActive == true;

    public bool ShowInstall => _allowed.HasFlag(RowActions.Install) && !Package.IsInstalled && !IsBusy && !Succeeded(OperationKind.Install);

    public bool ShowUpdate => _allowed.HasFlag(RowActions.Update) && Package.HasUpdate && !IsBusy && !Succeeded(OperationKind.Upgrade)
        && !IsRestartPending;

    /// <summary>App Installer updated, but the new version starts with CartonStash (004 R6.2). Rows are rebuilt when it changes.</summary>
    public bool IsRestartPending => _actions.Host.IsPending(Package);

    /// <summary>Offered where Update would be (004 R6.2, R6.3).</summary>
    public bool ShowRestart => _allowed.HasFlag(RowActions.Update) && IsRestartPending && !IsBusy;

    public bool ShowUninstall => _allowed.HasFlag(RowActions.Uninstall) && Package.IsInstalled && Package.IsManaged
        && !IsBusy && !Succeeded(OperationKind.Uninstall);

    public bool IsExcluded => _exclusions?.Current.IsExcluded(Id) == true;

    public bool ShowExclude => _exclusions is not null && !IsExcluded;

    public bool ShowIncludeAgain => IsExcluded;

    /// <summary>"Installed", "Failed"… after the last operation ended; null otherwise or when cancelled.</summary>
    public string? ResultText => IsRestartPending && !IsBusy
        ? "Restart CartonStash to finish"
        : Operation is { IsActive: false, Result: not OperationResult.Cancelled } op ? op.StatusText : null;

    public bool HasResult => ResultText is not null;

    [RelayCommand]
    private Task InstallAsync() => _actions.InstallAsync(Package);

    [RelayCommand]
    private Task UninstallAsync() => _actions.UninstallAsync(Package);

    [RelayCommand]
    private Task UpdateAsync() => _actions.UpdateAsync(Package);

    [RelayCommand]
    private void Restart() => _actions.Queue.RequestRestart();

    /// <summary>A version chosen in the details pane, with the row's actions (014 R1.1).</summary>
    public Task InstallVersionAsync(PackageVersion version) => _actions.InstallVersionAsync(Package, version);

    [RelayCommand]
    private void Exclude() => _exclusions?.Update(s => s.WithExcluded(Id));

    [RelayCommand]
    private void IncludeAgain() => _exclusions?.Update(s => s.WithoutExcluded(Id));

    private bool Succeeded(OperationKind kind) => Operation is { IsSuccess: true } op && op.Kind == kind;

    partial void OnOperationChanged(PackageOperation? oldValue, PackageOperation? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.PropertyChanged -= OnOperationPropertyChanged;
        }
        if (newValue is not null)
        {
            newValue.PropertyChanged += OnOperationPropertyChanged;
        }
    }

    private void OnOperationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PackageOperation.Phase) or nameof(PackageOperation.Result))
        {
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(ShowInstall));
            OnPropertyChanged(nameof(ShowUpdate));
            OnPropertyChanged(nameof(ShowRestart));
            OnPropertyChanged(nameof(ShowUninstall));
            OnPropertyChanged(nameof(ResultText));
            OnPropertyChanged(nameof(HasResult));
        }
    }
}

/// <summary>Keeps a list's rows pointing at the latest operation for their package.</summary>
internal static class RowOperationTracker
{
    public static void Track(PackageActions actions, Func<IEnumerable<PackageRowViewModel>> rows)
    {
        actions.Queue.Enqueued += (_, operation) =>
        {
            foreach (var row in rows())
            {
                if (string.Equals(row.Package.Key, operation.Package.Key, StringComparison.OrdinalIgnoreCase))
                {
                    row.Operation = operation;
                }
            }
        };
    }
}
