using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using CartonStash.Core.Models;
using CartonStash.Core.Services;
using CartonStash.Core.ViewModels;

namespace CartonStash.App.Services;

/// <summary>ContentDialogs for confirmations and agreements (003 R1.3, R2.2, R6.2; 004 R2.3; 006 R3.3).</summary>
public sealed class DialogService : IDialogService
{
    // WinUI allows only one open ContentDialog at a time.
    private readonly SemaphoreSlim _oneAtATime = new(1, 1);

    /// <summary>Set by the main window once its content is loaded.</summary>
    public XamlRoot? XamlRoot { get; set; }

    public async Task<bool> ConfirmAsync(string title, string message, string confirmText, string cancelText = "Cancel")
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = confirmText,
            CloseButtonText = cancelText,
            DefaultButton = ContentDialogButton.Close, // destructive actions don't default to Yes
        };
        return await ShowAsync(dialog) == ContentDialogResult.Primary;
    }

    public async Task<bool> ShowAgreementsAsync(string title, IReadOnlyList<Agreement> agreements)
    {
        var panel = new StackPanel { Spacing = 12 };
        foreach (var agreement in agreements)
        {
            panel.Children.Add(new TextBlock { Text = agreement.Label, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
            if (agreement.Text is not null)
            {
                panel.Children.Add(new TextBlock { Text = agreement.Text, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true });
            }
            if (PackageDetailsViewModel.SafeUri(agreement.Url) is { } uri)
            {
                var link = new HyperlinkButton { Content = $"Read {agreement.Label} (opens browser)", Padding = new Thickness(0) };
                link.Click += async (_, _) => await Launcher.LaunchUriAsync(uri);
                panel.Children.Add(link);
            }
        }

        var dialog = new ContentDialog
        {
            Title = title,
            Content = new ScrollViewer { Content = panel, MaxHeight = 400 },
            PrimaryButtonText = "Accept",
            CloseButtonText = "Decline",
            DefaultButton = ContentDialogButton.Close,
        };
        return await ShowAsync(dialog) == ContentDialogResult.Primary;
    }

    private async Task<ContentDialogResult> ShowAsync(ContentDialog dialog)
    {
        await _oneAtATime.WaitAsync();
        try
        {
            dialog.XamlRoot = XamlRoot ?? throw new InvalidOperationException("The main window isn't ready yet.");
            dialog.RequestedTheme = (XamlRoot.Content as FrameworkElement)?.RequestedTheme ?? ElementTheme.Default;
            return await dialog.ShowAsync();
        }
        finally
        {
            _oneAtATime.Release();
        }
    }
}

/// <summary>Copies diagnostic text (006 R4.2).</summary>
public sealed class ClipboardService : IClipboardService
{
    public void SetText(string text)
    {
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
        Clipboard.Flush(); // keep it available after the app exits
    }
}

/// <summary>Applies the theme setting to the window and its title bar, live (006 R1.1).</summary>
public sealed class ThemeService(ISettings settings)
{
    private Window? _window;

    public void Attach(Window window)
    {
        _window = window;
        settings.Changed += (_, _) => Apply();
        Apply();
    }

    private void Apply()
    {
        if (_window?.Content is not FrameworkElement root)
        {
            return;
        }

        var theme = settings.Current.Theme;
        root.RequestedTheme = theme switch
        {
            AppTheme.Light => ElementTheme.Light,
            AppTheme.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
        _window.AppWindow.TitleBar.PreferredTheme = theme switch
        {
            AppTheme.Light => TitleBarTheme.Light,
            AppTheme.Dark => TitleBarTheme.Dark,
            _ => TitleBarTheme.UseDefaultAppMode,
        };
    }
}
