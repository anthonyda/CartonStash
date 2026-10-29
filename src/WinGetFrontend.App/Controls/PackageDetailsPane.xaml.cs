using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;
using WinGetFrontend.Core.ViewModels;

namespace WinGetFrontend.App.Controls;

public sealed partial class PackageDetailsPane : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(PackageDetailsViewModel), typeof(PackageDetailsPane), new PropertyMetadata(null));

    public PackageDetailsPane()
    {
        InitializeComponent();
    }

    public PackageDetailsViewModel? ViewModel
    {
        get => (PackageDetailsViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    // 005 R1.3: default browser, no embedded web content. The view model only exposes http(s) URIs.
    private async void Link_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: Uri uri })
        {
            await Launcher.LaunchUriAsync(uri);
        }
    }
}
