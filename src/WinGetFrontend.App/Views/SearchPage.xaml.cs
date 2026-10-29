using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using WinGetFrontend.Core.ViewModels;

namespace WinGetFrontend.App.Views;

public sealed partial class SearchPage : Page
{
    public SearchPage()
    {
        ViewModel = App.Services.GetRequiredService<SearchViewModel>();
        InitializeComponent();
        DetailsBehavior.Attach(this, ResultsList, ViewModel.Details);
    }

    public SearchViewModel ViewModel { get; }

    private void QueryBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && ViewModel.SearchCommand.CanExecute(null))
        {
            ViewModel.SearchCommand.Execute(null);
        }
    }
}
