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

    /// <summary>Smaller saved sizes are ignored (006 R6).</summary>
    private const int MinRestoreWidth = 500, MinRestoreHeight = 400;

    private readonly ISettings _settings;
    private bool _closeConfirmed;
    private bool _checkedAgreements;
    private Windows.Graphics.RectInt32 _normalBounds;

    public MainWindow(ShellViewModel shell, ActivityViewModel activity, DialogService dialogs, ThemeService theme,
        SourceAgreementCheck agreements, ISettings settings)
    {
        Shell = shell;
        Activity = activity;
        _settings = settings;
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        RestoreLayout(settings.Current);
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico")); // taskbar + Alt+Tab
        AppWindow.Closing += AppWindow_Closing;

        Root.Loaded += (_, _) =>
        {
            dialogs.XamlRoot = Root.XamlRoot;
            ApplyActivityPin();

            // Asks about source agreements first, then loads installed data so the Updates badge appears (006 R3.4, 004 R4.1).
            if (!_checkedAgreements)
            {
                _checkedAgreements = true;
                _ = agreements.RunAsync();
            }
        };
        Root.SizeChanged += (_, _) => ApplyActivityPin();
        ActivitySplit.PaneClosing += (_, e) => e.Cancel = ActivitySplit.DisplayMode == SplitViewDisplayMode.Inline; // 009 R3.2
        activity.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ActivityViewModel.IsPinned))
            {
                ApplyActivityPin();
            }
            else if (e.PropertyName == nameof(ActivityViewModel.Tab))
            {
                SelectActivityTab(); // Clear filter goes back to All (009 R4.7)
            }
        };
        theme.Attach(this);

        OpenStartPage(settings.Current.StartPage);
    }

    public ShellViewModel Shell { get; }

    public ActivityViewModel Activity { get; }

    /// <summary>Opens the Updates page, for a clicked notification (010 R2.2).</summary>
    public void ShowUpdates() => OpenStartPage(StartPage.Updates);

    /// <summary>The open page's navigation key, so a restart opens it again (004 R6.3).</summary>
    public string? CurrentPageKey => Pages.FirstOrDefault(p => p.Value == ContentFrame.CurrentSourcePageType).Key;

    /// <summary>Opens a page by navigation key; an unknown key leaves the start page (004 R6.3).</summary>
    public void OpenPage(string? key)
    {
        if (key is null || !Pages.TryGetValue(key, out var page))
        {
            return;
        }
        Nav.SelectedItem = key == "settings"
            ? Nav.SettingsItem
            : Nav.MenuItems.OfType<NavigationViewItem>().First(i => (string)i.Tag == key);
        if (ContentFrame.CurrentSourcePageType != page)
        {
            ContentFrame.Navigate(page);
        }
    }

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
        if (ContentFrame.CurrentSourcePageType != Pages[key])
        {
            ContentFrame.Navigate(Pages[key]);
        }
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

    /// <summary>Below this window width a pinned pane acts as an overlay (009 R3.5).</summary>
    private const double PinnedPaneMinWindowWidth = 1000;

    // 009 R3: inline beside the page while pinned and there's room, otherwise today's overlay.
    private void ApplyActivityPin()
    {
        var inline = Activity.IsPinned && Root.ActualWidth >= PinnedPaneMinWindowWidth;
        var mode = inline ? SplitViewDisplayMode.Inline : SplitViewDisplayMode.Overlay;
        if (ActivitySplit.DisplayMode == mode)
        {
            return;
        }

        ActivitySplit.DisplayMode = mode;
        ActivitySplit.IsPaneOpen = inline;
    }

    private void CloseActivity_Click(object sender, RoutedEventArgs e) => ActivitySplit.IsPaneOpen = false;

    // 009 R4.3
    private void ActivityTabs_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (sender.SelectedItem?.Tag is string tag && Enum.TryParse<ActivityTab>(tag, out var tab))
        {
            Activity.Tab = tab;
        }
    }

    private void SelectActivityTab()
    {
        var item = ActivityTabs.Items.FirstOrDefault(i => i.Tag as string == Activity.Tab.ToString());
        if (item is not null && ActivityTabs.SelectedItem != item)
        {
            ActivityTabs.SelectedItem = item;
        }
    }

    // 009 R4.5: the menu marks the current order.
    private void ActivitySort_Opening(object? sender, object e)
    {
        foreach (var item in ((MenuFlyout)sender!).Items.OfType<RadioMenuFlyoutItem>())
        {
            item.IsChecked = item.Tag as string == Activity.Sort.ToString();
        }
    }

    private void ActivitySort_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is string tag && Enum.TryParse<ActivitySort>(tag, out var sort))
        {
            Activity.Sort = sort;
        }
    }

    // 009 R4.10: Esc empties the filter.
    private void ActivityFilter_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape && Activity.FilterText.Length > 0)
        {
            Activity.FilterText = "";
            e.Handled = true;
        }
    }

    // 009 R4.10: Tab goes from card to card, past the dividers.
    private void ActivityList_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args) =>
        args.ItemContainer.IsTabStop = args.Item is not ActivityHeader;

    // 006 R6.2–R6.4: saved size, position, maximized state and navigation pane, or the defaults.
    private void RestoreLayout(AppSettings saved)
    {
        Nav.IsPaneOpen = saved.NavPaneOpen;

        if (saved.Window is not { Width: >= MinRestoreWidth, Height: >= MinRestoreHeight } w)
        {
            AppWindow.Resize(new Windows.Graphics.SizeInt32(1200, 780));
        }
        else
        {
            var rect = new Windows.Graphics.RectInt32(w.X, w.Y, w.Width, w.Height);
            if (DisplayArea.GetFromRect(rect, DisplayAreaFallback.None) is null)
            {
                AppWindow.Resize(new Windows.Graphics.SizeInt32(w.Width, w.Height)); // R6.3: off every display
            }
            else
            {
                AppWindow.MoveAndResize(rect);
            }
        }

        _normalBounds = CurrentBounds();
        if (saved.Window?.Maximized == true && AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.Maximize();
        }

        // Track the normal bounds so a maximized window still saves its restore size (R6.2).
        AppWindow.Changed += (sender, args) =>
        {
            if ((args.DidSizeChange || args.DidPositionChange) && sender.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Restored })
            {
                _normalBounds = CurrentBounds();
            }
        };
    }

    private Windows.Graphics.RectInt32 CurrentBounds() =>
        new(AppWindow.Position.X, AppWindow.Position.Y, AppWindow.Size.Width, AppWindow.Size.Height);

    // 006 R6.1
    private void SaveLayout()
    {
        var maximized = AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Maximized };
        var b = _normalBounds;
        _settings.Update(s => s with
        {
            Window = new WindowLayout(b.X, b.Y, b.Width, b.Height, maximized),
            // Compact and minimal modes close the pane on their own; keep the user's choice from expanded mode.
            NavPaneOpen = Nav.DisplayMode == NavigationViewDisplayMode.Expanded ? Nav.IsPaneOpen : s.NavPaneOpen,
        });
    }

    // 003 R6.2: warn before quitting with operations queued or running.
    private async void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_closeConfirmed || !Shell.HasPendingWork)
        {
            SaveLayout();
            return;
        }

        args.Cancel = true;
        if (await Shell.ConfirmCloseAsync())
        {
            SaveLayout();
            _closeConfirmed = true;
            Close();
        }
    }
}
