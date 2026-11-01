using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CartonStash.Core.ViewModels;

/// <summary>Shown in place of the app while WinGet is missing or won't start (011 R3).</summary>
/// <param name="startFailed">True when WinGet is installed but couldn't be started (R3.5).</param>
/// <param name="isInstalled">Checks for WinGet. Must not activate it.</param>
/// <param name="openApp">Opens the app. Returns false when WinGet still can't be started.</param>
public sealed partial class WinGetSetupViewModel(bool startFailed, Func<bool> isInstalled, Func<bool> openApp) : ObservableObject
{
    public string Title { get; } = startFailed ? "WinGet couldn't start" : "CartonStash needs WinGet";

    public string Message { get; } = startFailed
        ? "App Installer is on this PC, but WinGet didn't start. Update or repair App Installer, then choose Check again."
        : "WinGet comes with App Installer from Microsoft. Install it, then choose Check again.";

    /// <summary>The result of the latest Check again; null until there is one.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    public partial string? Status { get; private set; }

    /// <summary>A plain bool for the view: x:Bind doesn't call a function again once its argument is null (T8).</summary>
    public bool HasStatus => Status is not null;

    [RelayCommand]
    private void CheckAgain()
    {
        if (!isInstalled())
        {
            Status = "WinGet still wasn't found.";
        }
        else if (!openApp())
        {
            Status = "WinGet is installed but didn't start. Restart CartonStash, and if that doesn't help, restart your PC."; // R3.4
        }
        else
        {
            Status = null;
        }
    }
}
