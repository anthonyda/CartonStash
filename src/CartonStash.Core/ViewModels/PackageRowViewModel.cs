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

    // BR-04: a success hides its button only between the operation ending and the row showing WinGet's data again. Set
    // when this row sees its operation succeed; a row built later, or refreshed by a reload, follows the package alone.
    private bool _successHidesButtons;

    /// <param name="exclusions">Given where a row can be excluded from update checks: the Updates page (010 R5.1).</param>
    public PackageRowViewModel(PackageInfo package, PackageActions actions, RowActions allowed, ISettings? exclusions = null)
    {
        Package = package;
        _actions = actions;
        _allowed = allowed;
        _exclusions = exclusions;
        Operation = actions.Queue.Find(package);
    }

    public PackageInfo Package { get; private set; }

    /// <summary>
    /// 014 R2.6: the package as WinGet lists it now. Every property reads <see cref="Package"/>, so all of them change
    /// (an empty name, which x:Bind treats as "all"). The row stays the same object, so the details pane and the
    /// page's list keep it.
    /// </summary>
    public void Refresh(PackageInfo package)
    {
        var hidden = _successHidesButtons;
        _successHidesButtons = false; // BR-04: the list shows WinGet's data again
        if (package == Package)
        {
            if (hidden)
            {
                NotifyButtons();
            }
            return;
        }
        Package = package;
        OnPropertyChanged(string.Empty);
    }

    /// <summary>
    /// Re-reads everything that doesn't come from <see cref="Package"/>: App Installer's restart state (004 R6.2) and the
    /// exclusion (010 R5.5). The Updates page keeps its rows across reloads (004 R8.1), so it calls this instead of
    /// making new rows.
    /// </summary>
    public void NotifyStateChanged() => OnPropertyChanged(string.Empty);

    /// <summary>014 R2.6: after a success, read the package again; a failed read leaves the row as it is.</summary>
    private async Task RefreshAfterSuccessAsync()
    {
        try
        {
            if (await _actions.ReadInstalledAsync(Package) is { } fresh)
            {
                Refresh(fresh);
            }
        }
        catch (Exception)
        {
            // never an error of its own (013 R2.3)
        }
    }

    public string Id => Package.Id;

    public string Name => Package.Name;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy), nameof(ShowInstall), nameof(ShowUpdate), nameof(ShowRestart), nameof(ShowUninstall), nameof(ResultText), nameof(HasResult), nameof(ShowRetryAsAdmin))]
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

    /// <summary>App Installer updated, but the new version starts with CartonStash (004 R6.2). The Updates page re-notifies its rows when it changes.</summary>
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

    /// <summary>017 R1.3: next to the result, after a failure for lack of administrator rights.</summary>
    public bool ShowRetryAsAdmin => Operation?.CanRetryAsAdmin == true;

    [RelayCommand]
    private void RetryAsAdmin() => Operation?.RetryAsAdminCommand.Execute(null);

    [RelayCommand]
    private Task InstallAsync() => _actions.InstallAsync(Package);

    [RelayCommand]
    private Task UninstallAsync() => _actions.UninstallAsync(Package);

    [RelayCommand]
    private Task UpdateAsync() => _actions.UpdateAsync(Package);

    [RelayCommand]
    private void Restart() => _actions.Queue.RequestRestart();

    /// <summary>A version chosen in the details pane, with the row's actions (014 R1.1).</summary>
    public Task InstallVersionAsync(PackageVersion version, InstallScope? scope = null) =>
        _actions.InstallVersionAsync(Package, version, Refresh, scope); // 014 R2.7: Refresh when the list is stale; 015 R1.3: the scope

    /// <summary>015 R1.1: Install from the pane's split button, in one scope.</summary>
    public Task InstallInScopeAsync(InstallScope scope) => _actions.InstallAsync(Package, scope);

    [RelayCommand]
    private void Exclude() => _exclusions?.Update(s => s.WithExcluded(Id));

    [RelayCommand]
    private void IncludeAgain() => _exclusions?.Update(s => s.WithoutExcluded(Id));

    private bool Succeeded(OperationKind kind) => _successHidesButtons && Operation is { IsSuccess: true } op && op.Kind == kind;

    partial void OnOperationChanged(PackageOperation? oldValue, PackageOperation? newValue)
    {
        _successHidesButtons = false;
        if (oldValue is not null)
        {
            oldValue.PropertyChanged -= OnOperationPropertyChanged;
        }
        if (newValue is not null)
        {
            newValue.PropertyChanged += OnOperationPropertyChanged;
        }
    }

    private void NotifyButtons()
    {
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(ShowInstall));
        OnPropertyChanged(nameof(ShowUpdate));
        OnPropertyChanged(nameof(ShowRestart));
        OnPropertyChanged(nameof(ShowUninstall));
        OnPropertyChanged(nameof(ResultText));
        OnPropertyChanged(nameof(HasResult));
        OnPropertyChanged(nameof(ShowRetryAsAdmin));
    }

    private void OnOperationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PackageOperation.Phase) or nameof(PackageOperation.Result))
        {
            if (sender == Operation && Operation is { IsActive: false, IsSuccess: true })
            {
                _successHidesButtons = true; // this row saw it run
            }
            NotifyButtons();
        }
        if (e.PropertyName == nameof(PackageOperation.Result) && sender is PackageOperation { IsActive: false, IsSuccess: true } op && op == Operation)
        {
            _ = RefreshAfterSuccessAsync();
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
                if (row.Package.IsSamePackage(operation.Package)) // BR-07
                {
                    row.Operation = operation;
                }
            }
        };
    }
}
