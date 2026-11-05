using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace CartonStash.App;

/// <summary>
/// Shown by the new copy after a restart for App Installer's update (004 R6.3), while it waits for WinGet's old server to
/// stop. Nothing here touches WinGet.
/// </summary>
public sealed class FinishUpdateWindow : Window
{
    public FinishUpdateWindow()
    {
        Title = "CartonStash";
        SystemBackdrop = new MicaBackdrop();
        AppWindow.Resize(new Windows.Graphics.SizeInt32(480, 200));
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        var text = new TextBlock
        {
            Text = "Finishing the App Installer update…",
            VerticalAlignment = VerticalAlignment.Center,
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
        };
        var ring = new ProgressRing { IsActive = true, Width = 24, Height = 24 };
        AutomationProperties.SetName(ring, text.Text);
        Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                ring,
                text,
            },
        };
    }
}
