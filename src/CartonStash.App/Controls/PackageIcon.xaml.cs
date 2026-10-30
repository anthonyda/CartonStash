using System.ComponentModel;
using CartonStash.Core.Models;
using CartonStash.Core.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace CartonStash.App.Controls;

/// <summary>
/// Shows a row's icon. Set explicitly in code, not with an x:Bind function: lists recycle row controls, and a
/// recycled control must drop the previous package's picture when the new package has no icon (005 R5.1).
/// </summary>
public sealed partial class PackageIcon : UserControl
{
    public static readonly DependencyProperty RowProperty =
        DependencyProperty.Register(nameof(Row), typeof(PackageRowViewModel), typeof(PackageIcon), new PropertyMetadata(null, OnRowChanged));

    /// <summary>For places without a row, such as the Activity pane (009 R1.2).</summary>
    public static readonly DependencyProperty PackageProperty =
        DependencyProperty.Register(nameof(Package), typeof(PackageInfo), typeof(PackageIcon), new PropertyMetadata(null, OnPackageChanged));

    public PackageIcon()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Glyph.FontSize = Math.Max(12, ActualHeight * 0.7);
    }

    public PackageRowViewModel? Row
    {
        get => (PackageRowViewModel?)GetValue(RowProperty);
        set => SetValue(RowProperty, value);
    }

    public PackageInfo? Package
    {
        get => (PackageInfo?)GetValue(PackageProperty);
        set => SetValue(PackageProperty, value);
    }

    private static async void OnPackageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (PackageIcon)d;
        control.Show(null);
        if (e.NewValue is not PackageInfo package)
        {
            return;
        }

        var uri = await App.Services.GetRequiredService<PackageActions>().GetIconAsync(package);
        if (ReferenceEquals(control.Package, package)) // recycled meanwhile: drop the late result
        {
            control.Show(uri);
        }
    }

    private static void OnRowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (PackageIcon)d;
        if (e.OldValue is PackageRowViewModel oldRow)
        {
            oldRow.PropertyChanged -= control.OnRowPropertyChanged;
        }
        if (e.NewValue is PackageRowViewModel newRow)
        {
            newRow.PropertyChanged += control.OnRowPropertyChanged;
            // Only rows on screen get a control, so icons load for those only (005 R5.2).
            _ = newRow.EnsureIconAsync();
        }
        control.Show((e.NewValue as PackageRowViewModel)?.IconUri);
    }

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Ignore late results for a row this control no longer shows.
        if (e.PropertyName == nameof(PackageRowViewModel.IconUri) && ReferenceEquals(sender, Row))
        {
            Show(Row?.IconUri);
        }
    }

    private void Show(Uri? uri)
    {
        Picture.Source = uri is null
            ? null
            : new BitmapImage { UriSource = uri, DecodePixelWidth = (int)Math.Max(Width is > 0 ? Width : 32, 16), DecodePixelType = DecodePixelType.Logical };
        Picture.Visibility = uri is null ? Visibility.Collapsed : Visibility.Visible;
        Glyph.Visibility = uri is null ? Visibility.Visible : Visibility.Collapsed;
    }
}
