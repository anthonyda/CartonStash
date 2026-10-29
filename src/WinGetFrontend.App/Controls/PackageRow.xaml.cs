using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinGetFrontend.Core.ViewModels;

namespace WinGetFrontend.App.Controls;

public sealed partial class PackageRow : UserControl
{
    public static readonly DependencyProperty RowProperty =
        DependencyProperty.Register(nameof(Row), typeof(PackageRowViewModel), typeof(PackageRow), new PropertyMetadata(null, OnRowChanged));

    public PackageRow()
    {
        InitializeComponent();
    }

    public PackageRowViewModel? Row
    {
        get => (PackageRowViewModel?)GetValue(RowProperty);
        set => SetValue(RowProperty, value);
    }

    // The list only creates row controls for rows on screen, so icons load for those only (005 R5.2).
    private static void OnRowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        _ = (e.NewValue as PackageRowViewModel)?.EnsureIconAsync();
}
