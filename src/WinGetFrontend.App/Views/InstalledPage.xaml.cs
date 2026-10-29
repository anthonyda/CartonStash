using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WinGetFrontend.Core.ViewModels;

namespace WinGetFrontend.App.Views;

public sealed partial class InstalledPage : Page
{
    public InstalledPage()
    {
        ViewModel = App.Services.GetRequiredService<InstalledViewModel>();
        InitializeComponent();
        DetailsBehavior.Attach(this, PackagesList, ViewModel.Details);
    }

    public InstalledViewModel ViewModel { get; }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadAsync(); // first visit only (002 R1.3)
    }
}
