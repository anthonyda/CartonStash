using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using CartonStash.Core.ViewModels;

namespace CartonStash.App.Views;

public sealed partial class UpdatesPage : Page
{
    public UpdatesPage()
    {
        ViewModel = App.Services.GetRequiredService<UpdatesViewModel>();
        InitializeComponent();
        DetailsBehavior.Attach(this, UpdatesList, ViewModel.Details, DetailsPane);
    }

    public UpdatesViewModel ViewModel { get; }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadAsync();
    }

    // ListView.SelectedItems isn't bindable; hand the checked rows to the view model (004 R2.2).
    private void UpdatesList_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ViewModel.SetSelection(UpdatesList.SelectedItems.OfType<PackageRowViewModel>().ToList());
}
