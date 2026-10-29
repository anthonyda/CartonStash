using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinGetFrontend.Core.Models;
using WinGetFrontend.Core.Services;

namespace WinGetFrontend.Core.ViewModels;

/// <summary>Walking-skeleton search screen. Behaviour is defined in specs/001-package-search.</summary>
public sealed partial class SearchViewModel(IPackageService packages) : ObservableObject
{
    public ObservableCollection<PackageInfo> Results { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchCommand))]
    public partial string Query { get; set; } = "";

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    private bool CanSearch() => !string.IsNullOrWhiteSpace(Query);

    [RelayCommand(CanExecute = nameof(CanSearch), IncludeCancelCommand = true)]
    private async Task SearchAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        Results.Clear();
        try
        {
            foreach (var package in await packages.SearchAsync(Query, cancellationToken: cancellationToken))
            {
                Results.Add(package);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
