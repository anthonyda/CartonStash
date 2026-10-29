using CommunityToolkit.Mvvm.ComponentModel;
using WinGetFrontend.Core.Models;

namespace WinGetFrontend.Core.Services;

/// <summary>
/// The installed-package list, loaded once per session (002 R1.3) and shared by the Installed and Updates
/// pages (004 R1.2). Reloads after Refresh, and once the operation queue drains if anything succeeded.
/// Use it from the UI thread.
/// </summary>
public sealed partial class InstalledPackagesStore : ObservableObject
{
    private readonly IPackageService _packages;
    private CancellationTokenSource? _currentLoad;
    private Task? _firstLoad;
    private bool _changedSinceLoad;

    public InstalledPackagesStore(IPackageService packages, PackageOperationQueue queue)
    {
        _packages = packages;
        queue.Completed += (_, operation) => _changedSinceLoad |= operation.IsSuccess;
        queue.Drained += async (_, _) =>
        {
            if (_changedSinceLoad && IsLoaded)
            {
                await ReloadAsync();
            }
        };
    }

    [ObservableProperty]
    public partial IReadOnlyList<PackageInfo> Packages { get; private set; } = [];

    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    /// <summary>True once a load has finished (successfully or not).</summary>
    [ObservableProperty]
    public partial bool IsLoaded { get; private set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    /// <summary>Loads the list the first time it's needed; later calls reuse it (R1.3).</summary>
    public Task EnsureLoadedAsync() => _firstLoad ??= LoadAsync();

    /// <summary>Refresh: drops cached catalogs and loads again, superseding a running load.</summary>
    public Task ReloadAsync()
    {
        _packages.InvalidateCache();
        _firstLoad = LoadAsync();
        return _firstLoad;
    }

    private async Task LoadAsync()
    {
        _currentLoad?.Cancel();
        using var load = new CancellationTokenSource();
        _currentLoad = load;
        _changedSinceLoad = false;
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var installed = await _packages.GetInstalledAsync(load.Token);
            if (!load.IsCancellationRequested)
            {
                Packages = installed;
            }
        }
        catch (OperationCanceledException) when (load.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (!load.IsCancellationRequested)
        {
            ErrorMessage = ex is WinGetException ? ex.Message : $"Couldn't load installed packages: {ex.Message}";
        }
        finally
        {
            if (_currentLoad == load)
            {
                _currentLoad = null;
                IsLoading = false;
                IsLoaded = true;
            }
        }
    }
}
