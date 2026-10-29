using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WinGetFrontend.Core.Models;
using WinGetFrontend.Core.ViewModels;

namespace WinGetFrontend.App.Views;

public sealed partial class SettingsPage : Page
{
    private bool _loaded;

    public SettingsPage()
    {
        ViewModel = App.Services.GetRequiredService<SettingsViewModel>();
        InitializeComponent();
    }

    public SettingsViewModel ViewModel { get; }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
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
}
