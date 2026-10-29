using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CartonStash.Core.Models;

namespace CartonStash.Core.ViewModels;

/// <summary>
/// One row in Search / Installed / Updates (and the details pane header): the package plus the state of its
/// latest operation (003 R3.1). The owning page keeps <see cref="Operation"/> current.
/// </summary>
public sealed partial class PackageRowViewModel : ObservableObject
{
    private readonly PackageActions _actions;
    private readonly RowActions _allowed;
    private Task? _iconLoad;

    public PackageRowViewModel(PackageInfo package, PackageActions actions, RowActions allowed)
    {
        Package = package;
        _actions = actions;
        _allowed = allowed;
        Operation = actions.Queue.Find(package);
    }

    public PackageInfo Package { get; }

    public string Id => Package.Id;

    public string Name => Package.Name;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy), nameof(ShowInstall), nameof(ShowUpdate), nameof(ShowUninstall), nameof(ResultText), nameof(HasResult))]
    public partial PackageOperation? Operation { get; set; }

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

    public bool ShowUpdate => _allowed.HasFlag(RowActions.Update) && Package.HasUpdate && !IsBusy && !Succeeded(OperationKind.Upgrade);

    public bool ShowUninstall => _allowed.HasFlag(RowActions.Uninstall) && Package.IsInstalled && Package.IsManaged
        && !IsBusy && !Succeeded(OperationKind.Uninstall);

    /// <summary>"Installed", "Failed"… after the last operation ended; null otherwise or when cancelled.</summary>
    public string? ResultText => Operation is { IsActive: false, Result: not OperationResult.Cancelled } op ? op.StatusText : null;

    public bool HasResult => ResultText is not null;

    [RelayCommand]
    private Task InstallAsync() => _actions.InstallAsync(Package);

    [RelayCommand]
    private Task UninstallAsync() => _actions.UninstallAsync(Package);

    [RelayCommand]
    private void Update() => _actions.Update(Package);

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
