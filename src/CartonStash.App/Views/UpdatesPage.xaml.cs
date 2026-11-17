using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using CartonStash.Core.ViewModels;

namespace CartonStash.App.Views;

public sealed partial class UpdatesPage : Page, IShortcutTarget
{
    public UpdatesPage()
    {
        ViewModel = App.Services.GetRequiredService<UpdatesViewModel>();
        InitializeComponent();
        DetailsBehavior.Attach(this, ViewModel.Details, DetailsPane);
        DetailsBehavior.AttachList(UpdatesList, ViewModel.Details);
        DetailsBehavior.AttachList(ExcludedList, ViewModel.Details); // 014 R1.7
    }

    public UpdatesViewModel ViewModel { get; }

    public bool FocusSearch() => false; // no search or filter box (019 R2.2)

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
        await ViewModel.LoadAsync();
    }

    // ListView.SelectedItems isn't bindable; hand the checked rows to the view model (004 R2.2).
    private void UpdatesList_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ViewModel.SetSelection(UpdatesList.SelectedItems.OfType<PackageRowViewModel>().ToList());
}
