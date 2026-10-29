using CartonStash.Core.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace CartonStash.App.Controls;

/// <summary>
/// The "Installed" badge (002 R7). Set in code when the row changes, so recycled list rows always show their own state.
/// The tooltip and accessible name say the state in words; color is never the only cue (R7.3, constitution Q5).
/// </summary>
public sealed partial class VersionBadge : UserControl
{
    public static readonly DependencyProperty RowProperty =
        DependencyProperty.Register(nameof(Row), typeof(PackageRowViewModel), typeof(VersionBadge), new PropertyMetadata(null, OnRowChanged));

    public VersionBadge()
    {
        InitializeComponent();
    }

    public PackageRowViewModel? Row
    {
        get => (PackageRowViewModel?)GetValue(RowProperty);
        set => SetValue(RowProperty, value);
    }

    private static void OnRowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((VersionBadge)d).Show(e.NewValue as PackageRowViewModel);

    private void Show(PackageRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        Label.Text = row.VersionBadgeText;
        Chip.Style = (Style)Resources[row.VersionBadge switch
        {
            Core.ViewModels.VersionBadge.Same => "SameStyle",
            Core.ViewModels.VersionBadge.UpdateOffered => "UpdateOfferedStyle",
            Core.ViewModels.VersionBadge.Newer => "NewerStyle",
            _ => "NeutralStyle",
        }];
        ToolTipService.SetToolTip(this, row.VersionStatusText);
        AutomationProperties.SetName(this, row.VersionStatusText);
    }
}
