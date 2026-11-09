using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CartonStash.Core.Models;

namespace CartonStash.Core.ViewModels;

/// <summary>
/// A version as the pane's list shows it: "0.102.0 (installed)" (005 R2.1, 014 R1.1). The selected one carries the
/// action button on its own line (014 R1.6), so the template binds plain values (x:Bind gotcha).
/// </summary>
public sealed partial class VersionItem(PackageVersion version, string label, string packageName, IAsyncRelayCommand run)
    : ObservableObject
{
    public PackageVersion Version { get; } = version;

    public string Label { get; } = label;

    /// <summary>The pane's command: it runs the selected version, and its CanExecute follows 003 R4.2.</summary>
    public IAsyncRelayCommand RunCommand { get; } = run;

    /// <summary>Set by the pane for the one selected item (014 R1.6).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowAction))]
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
