using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CartonStash.Core.Models;

namespace CartonStash.Core.ViewModels;

/// <summary>
/// A version as the pane's list shows it: "0.102.0 (installed)" (005 R2.1, 014 R1.1). The selected one carries the
/// action button on its own line (014 R1.6), so the template binds plain values (x:Bind gotcha).
/// </summary>
public sealed partial class VersionItem(PackageVersion version, string label, string packageName, IAsyncRelayCommand run,
    IAsyncRelayCommand? runForAllUsers = null, IAsyncRelayCommand? runForCurrentUser = null)
    : ObservableObject
{
    public PackageVersion Version { get; } = version;

    /// <summary>015 R1.3: the pane's commands for an install of this version in one scope.</summary>
    public IAsyncRelayCommand? RunForAllUsersCommand { get; } = runForAllUsers;

    public IAsyncRelayCommand? RunForCurrentUserCommand { get; } = runForCurrentUser;

    /// <summary>015 R1.3: set by the pane once WinGet said this version has both scopes; its Install is then a split button.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowScopeChoice), nameof(ShowPlainAction))]
    public partial bool OffersScopeChoice { get; set; }

    /// <summary>The split button on the selected line (015 R1.3). Plain bools for x:Bind.</summary>
    public bool ShowScopeChoice => ShowAction && OffersScopeChoice && Version.Direction == VersionDirection.NotInstalled;

    public bool ShowPlainAction => ShowAction && !ShowScopeChoice;

    /// <summary>015 R1.6: the menu items' accessible names.</summary>
    public string ForAllUsersName => $"Install {packageName} {Version.Version} for all users";

    public string ForCurrentUserName => $"Install {packageName} {Version.Version} for the current user";

    public string Label { get; } = label;

    /// <summary>The pane's command: it runs the selected version, and its CanExecute follows 003 R4.2.</summary>
    public IAsyncRelayCommand RunCommand { get; } = run;

    /// <summary>Set by the pane for the one selected item (014 R1.6).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowAction), nameof(ShowScopeChoice), nameof(ShowPlainAction))]
    public partial bool IsSelected { get; set; }

    /// <summary>The button shows on the selected line, except for the installed version (014 R1.1, R1.6).</summary>
    public bool ShowAction => IsSelected && Version.Direction != VersionDirection.Installed;

    public string ActionText => Version.Direction switch
    {
        VersionDirection.NotInstalled => "Install",
        VersionDirection.Newer => "Update to this version",
        VersionDirection.Older => "Go back to this version",
        _ => "Install this version",
    };

    /// <summary>The button's accessible name, with the package and the version (014 R1.4).</summary>
    public string ActionName => Version.Direction switch
    {
        VersionDirection.Newer => $"Update {packageName} to {Version.Version}",
        VersionDirection.Older => $"Go back to {packageName} {Version.Version}",
        _ => $"Install {packageName} {Version.Version}",
    };

    public override string ToString() => Label;
}
