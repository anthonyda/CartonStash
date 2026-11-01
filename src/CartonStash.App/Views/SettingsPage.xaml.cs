using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using CartonStash.Core.Models;
using CartonStash.Core.ViewModels;

namespace CartonStash.App.Views;

public sealed partial class SettingsPage : Page
{
    private bool _loaded;

    public SettingsPage()
    {
        ViewModel = App.Services.GetRequiredService<SettingsViewModel>();
        Checks = App.Services.GetRequiredService<CheckSettingsViewModel>();
        InitializeComponent();
    }

    public SettingsViewModel ViewModel { get; }

    public CheckSettingsViewModel Checks { get; }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        Checks.Refresh();
        if (!_loaded)
        {
            _loaded = true;
            await ViewModel.LoadCommand.ExecuteAsync(null);
        }
    }

    // Item templates can't reach the page's view model with x:Bind; forward the clicked source.
    private void ReviewAgreements_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: SourceInfo source })
        {
            ViewModel.ReviewAgreementsCommand.Execute(source);
        }
    }

    private void RemoveExclusion_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ExcludedPackageRow row })
        {
            Checks.RemoveExclusionCommand.Execute(row);
        }
    }
}
