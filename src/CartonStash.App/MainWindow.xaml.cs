using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CartonStash.App.Services;
using CartonStash.App.Views;
using CartonStash.Core.Models;
using CartonStash.Core.Services;
using CartonStash.Core.ViewModels;

namespace CartonStash.App;

public sealed partial class MainWindow : Window
{
    // Navigation keys → pages (002 R4.1). Settings is NavigationView's built-in item.
    private static readonly Dictionary<string, Type> Pages = new()
    {
        ["search"] = typeof(SearchPage),
        ["installed"] = typeof(InstalledPage),
        ["updates"] = typeof(UpdatesPage),
        ["settings"] = typeof(SettingsPage),
    };

    private bool _closeConfirmed;

    public MainWindow(ShellViewModel shell, ActivityViewModel activity, DialogService dialogs, ThemeService theme,
        InstalledPackagesStore installed, ISettings settings)
    {
        Shell = shell;
        Activity = activity;
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1200, 780));
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico")); // taskbar + Alt+Tab
        AppWindow.Closing += AppWindow_Closing;

        Root.Loaded += (_, _) => dialogs.XamlRoot = Root.XamlRoot;
        theme.Attach(this);

        OpenStartPage(settings.Current.StartPage);

        // Load installed data in the background so the Updates badge appears (004 R4.1).
        _ = installed.EnsureLoadedAsync();
    }

    public ShellViewModel Shell { get; }

    public ActivityViewModel Activity { get; }

    // 006 R5.2: open the saved start page and select its navigation item.
    private void OpenStartPage(StartPage start)
    {
        var key = start switch
        {
            StartPage.Installed => "installed",
            StartPage.Updates => "updates",
            _ => "search",
        };
        Nav.SelectedItem = Nav.MenuItems.OfType<NavigationViewItem>().First(i => (string)i.Tag == key);
        ContentFrame.Navigate(Pages[key]);
    }

    private void Nav_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        var key = args.IsSettingsInvoked ? "settings" : args.InvokedItemContainer?.Tag as string;
        if (key is not null &&Pages.TryGetValue(key, out var page) && ContentFrame.CurrentSourcePageType != page)
        {
            ContentFrame.Navigate(page);
        }
    }

    // Activity is selected by click, keyboard and UI Automation alike (assistive tech uses "Select", Q5):
    // open the pane and put the selection back on the current page.
    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer?.Tag as string != "activity")
        {
            return;
        }

        ActivitySplit.IsPaneOpen = true;
        sender.SelectedItem = ContentFrame.CurrentSourcePageType == typeof(SettingsPage)
            ? sender.SettingsItem
            : sender.MenuItems.OfType<NavigationViewItem>()
                .FirstOrDefault(i => i.Tag is string key && Pages[key] == ContentFrame.CurrentSourcePageType);
    }

    // Narrow windows collapse the navigation to a menu button over the content; keep page titles clear of it.
    private void Nav_DisplayModeChanged(NavigationView sender, NavigationViewDisplayModeChangedEventArgs args) =>
        ContentFrame.Margin = args.DisplayMode == NavigationViewDisplayMode.Minimal ? new Thickness(0, 40, 0, 0) : new Thickness(0);

    private void CloseActivity_Click(object sender, RoutedEventArgs e) => ActivitySplit.IsPaneOpen = false;

    // 003 R6.2: warn before quitting with operations queued or running.
    private async void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_closeConfirmed || !Shell.HasPendingWork)
        {
            return;
        }

        args.Cancel = true;
        if (await Shell.ConfirmCloseAsync())
        {
            _closeConfirmed = true;
            Close();
        }
    }
}
