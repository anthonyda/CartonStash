using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CartonStash.Core.ViewModels;

namespace CartonStash.App.Controls;

public sealed partial class PackageActionsView : UserControl
{
    public static readonly DependencyProperty RowProperty =
        DependencyProperty.Register(nameof(Row), typeof(PackageRowViewModel), typeof(PackageActionsView), new PropertyMetadata(null));

    public PackageActionsView()
    {
        InitializeComponent();
    }

    public PackageRowViewModel? Row
    {
        get => (PackageRowViewModel?)GetValue(RowProperty);
        set => SetValue(RowProperty, value);
    }

    // 015 R1.1, R1.7: only the details pane sets these; rows keep a plain Install button.
    public static readonly DependencyProperty OffersScopeChoiceProperty =
        DependencyProperty.Register(nameof(OffersScopeChoice), typeof(bool), typeof(PackageActionsView), new PropertyMetadata(false));

    public bool OffersScopeChoice
    {
        get => (bool)GetValue(OffersScopeChoiceProperty);
        set => SetValue(OffersScopeChoiceProperty, value);
    }

    public static readonly DependencyProperty InstallForAllUsersCommandProperty =
        DependencyProperty.Register(nameof(InstallForAllUsersCommand), typeof(ICommand), typeof(PackageActionsView), new PropertyMetadata(null));

    public ICommand? InstallForAllUsersCommand
    {
        get => (ICommand?)GetValue(InstallForAllUsersCommandProperty);
        set => SetValue(InstallForAllUsersCommandProperty, value);
    }

    public static readonly DependencyProperty InstallForCurrentUserCommandProperty =
        DependencyProperty.Register(nameof(InstallForCurrentUserCommand), typeof(ICommand), typeof(PackageActionsView), new PropertyMetadata(null));

    public ICommand? InstallForCurrentUserCommand
    {
        get => (ICommand?)GetValue(InstallForCurrentUserCommandProperty);
        set => SetValue(InstallForCurrentUserCommandProperty, value);
    }
}
