using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinGetFrontend.Core.ViewModels;

namespace WinGetFrontend.App.Controls;

public sealed partial class PackageRow : UserControl
{
    public static readonly DependencyProperty RowProperty =
        DependencyProperty.Register(nameof(Row), typeof(PackageRowViewModel), typeof(PackageRow), new PropertyMetadata(null));

    public PackageRow()
    {
        InitializeComponent();
    }

    public PackageRowViewModel? Row
    {
        get => (PackageRowViewModel?)GetValue(RowProperty);
        set => SetValue(RowProperty, value);
    }
}
