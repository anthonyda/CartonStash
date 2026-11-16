using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using CartonStash.Core.ViewModels;

namespace CartonStash.App.Views;

public sealed partial class SearchPage : Page, IShortcutTarget
{
    public SearchPage()
    {
        ViewModel = App.Services.GetRequiredService<SearchViewModel>();
        InitializeComponent();
        DetailsBehavior.Attach(this, ViewModel.Details, DetailsPane);
        DetailsBehavior.AttachList(ResultsList, ViewModel.Details);
    }

    public SearchViewModel ViewModel { get; }

    // 019 R2.1
    public bool FocusSearch()
    {
        QueryBox.Focus(FocusState.Keyboard);
        QueryBox.SelectAll();
        return true;
    }

    public bool Refresh() => false;

    private void QueryBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && ViewModel.SearchCommand.CanExecute(null))
        {
            ViewModel.SearchCommand.Execute(null);
        }
    }
}
