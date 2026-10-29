using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using WinGetFrontend.Core.ViewModels;

namespace WinGetFrontend.App;

public sealed partial class MainWindow : Window
{
    public SearchViewModel ViewModel { get; }

    public MainWindow(SearchViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1000, 700));
    }

    // Instance method: x:Bind function bindings are invoked on the page instance.
    private bool HasError(string? message) => !string.IsNullOrEmpty(message);

    private void QueryBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && ViewModel.SearchCommand.CanExecute(null))
        {
            ViewModel.SearchCommand.Execute(null);
        }
    }
}
