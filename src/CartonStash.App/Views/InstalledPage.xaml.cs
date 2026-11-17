using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using CartonStash.Core.ViewModels;

namespace CartonStash.App.Views;

public sealed partial class InstalledPage : Page, IShortcutTarget
{
    public InstalledPage()
    {
        ViewModel = App.Services.GetRequiredService<InstalledViewModel>();
        InitializeComponent();
        DetailsBehavior.Attach(this, ViewModel.Details, DetailsPane);
        DetailsBehavior.AttachList(PackagesList, ViewModel.Details);
    }

    public InstalledViewModel ViewModel { get; }

    // 019 R2.2
    public bool FocusSearch()
    {
        FilterBox.Focus(FocusState.Keyboard);
        FilterBox.SelectAll();
        return true;
    }

    // 019 R3.1, R3.2: a held F5 doesn't restart a running refresh.
    public bool Refresh()
    {
        if (!ViewModel.IsLoading)
        {
            ViewModel.RefreshCommand.Execute(null);
        }
        return true;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadAsync(); // first visit only (002 R1.3)
    }
}
