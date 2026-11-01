using Microsoft.UI.Xaml;
using CartonStash.Core.ViewModels;

namespace CartonStash.App;

/// <summary>Opens instead of <see cref="MainWindow"/> while WinGet is missing or won't start (011 R3).</summary>
public sealed partial class WinGetSetupWindow : Window
{
    public WinGetSetupWindow(WinGetSetupViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.Resize(new Windows.Graphics.SizeInt32(640, 420));
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));

        Root.Loaded += (_, _) => GetAppInstaller.Focus(FocusState.Programmatic);
    }

    public WinGetSetupViewModel ViewModel { get; }
}
