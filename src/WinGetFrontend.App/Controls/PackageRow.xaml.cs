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

    /// <summary>Below this row width the versions move under the name, so the name keeps room.</summary>
    private const double CompactWidth = 620;

    // View-only reflow: the row's own width matters here, not the window's (lists sit next to the details pane).
    private void Layout_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var compact = e.NewSize.Width < CompactWidth;
        Grid.SetRow(Versions, compact ? 1 : 0);
        Grid.SetColumn(Versions, compact ? 1 : 2);
        Grid.SetColumnSpan(Versions, compact ? 2 : 1);
        Versions.Margin = compact ? new Thickness(0, 4, 0, 0) : new Thickness(0);
        Versions.Orientation = compact ? Orientation.Horizontal : Orientation.Vertical;
        Versions.Spacing = compact ? 8 : 4;
        Versions.HorizontalAlignment = Badges.HorizontalAlignment = SourceLabel.HorizontalAlignment =
            compact ? HorizontalAlignment.Left : HorizontalAlignment.Right;
    }

    // The list only creates row controls for rows on screen, so icons load for those only (005 R5.2).
    private static void OnRowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        _ = (e.NewValue as PackageRowViewModel)?.EnsureIconAsync();
}
