using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CartonStash.Core.Services;

namespace CartonStash.Core.ViewModels;

/// <summary>Shown in place of the app while WinGet is missing or won't start (011 R3).</summary>
/// <param name="startFailed">True when WinGet is installed but couldn't be started (R3.5).</param>
/// <param name="isInstalled">Checks for WinGet. Must not activate it.</param>
/// <param name="openApp">Opens the app. Returns false when WinGet still can't be started.</param>
/// <param name="setUp">Installs App Installer (006 R3.7). Returns null, or a readable reason. Null when it isn't offered.</param>
public sealed partial class WinGetSetupViewModel(bool startFailed, Func<bool> isInstalled, Func<bool> openApp,
    Func<IProgress<string>, CancellationToken, Task<string?>>? setUp = null) : ObservableObject
{
    public string Title { get; } = startFailed ? "WinGet couldn't start" : "CartonStash needs WinGet";

    public string Message { get; } = startFailed
        ? "App Installer is on this PC, but WinGet didn't start. Update or repair App Installer, then choose Check again."
        : setUp is null
            ? "WinGet comes with App Installer from Microsoft. Install it, then choose Check again."
            : $"WinGet comes with App Installer from Microsoft. CartonStash can set it up for you, which downloads {AppInstallerSize} from Microsoft, or you can install it yourself and choose Check again.";

    private const string AppInstallerSize = AppInstallerSetup.DownloadSize;

    /// <summary>Set up for me is on offer: WinGet is missing, and this isn't a broken install (R3.5).</summary>
    public bool CanSetUp => setUp is not null && !startFailed;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SetUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(CheckAgainCommand))]
    public partial bool IsSettingUp { get; private set; }

    /// <summary>The result of the latest Check again; null until there is one.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    public partial string? Status { get; private set; }

    /// <summary>A plain bool for the view: x:Bind doesn't call a function again once its argument is null (T8).</summary>
    public bool HasStatus => Status is not null;

    private bool IsIdle() => !IsSettingUp;

    /// <summary>Only ever runs from the user's click (P2).</summary>
    [RelayCommand(CanExecute = nameof(IsIdle))]
    private async Task SetUpAsync()
    {
        IsSettingUp = true;
        string? error;
        try
        {
            error = await setUp!(new Progress<string>(text => Status = text), CancellationToken.None);
        }
        finally
        {
            IsSettingUp = false;
        }

        if (error is null)
        {
            CheckAgain();
        }
        else
        {
            Status = $"{error} You can still use Get App Installer.";
        }
    }

    [RelayCommand(CanExecute = nameof(IsIdle))]
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
