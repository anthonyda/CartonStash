using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using CartonStash.Core.ViewModels;

namespace CartonStash.App.Views;

public sealed partial class InstalledPage : Page
{
    public InstalledPage()
    {
        ViewModel = App.Services.GetRequiredService<InstalledViewModel>();
        InitializeComponent();
        DetailsBehavior.Attach(this, PackagesList, ViewModel.Details, DetailsPane);
    }

    public InstalledViewModel ViewModel { get; }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadAsync(); // first visit only (002 R1.3)
    }
}
