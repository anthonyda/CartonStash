using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using CartonStash.App.Services;
using CartonStash.Core.Services;
using CartonStash.Core.ViewModels;

namespace CartonStash.App;

public partial class App : Application
{
    private Window? _window;

    public static IServiceProvider Services { get; } = ConfigureServices();

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Resolved on the UI thread: the queue and store rely on its SynchronizationContext.
        _window = Services.GetRequiredService<MainWindow>();
        _window.Activate();
    }

    private static ServiceProvider ConfigureServices() =>
        new ServiceCollection()
            // WinGet (Core/Services is the only layer that touches it)
            .AddSingleton<CatalogConnection>()
            .AddSingleton<IPackageService, WinGetPackageService>()
            .AddSingleton<IPackageInstaller, WinGetPackageInstaller>()
            .AddSingleton<IOperationLogStore, OperationLogStore>()
            .AddSingleton<PackageOperationQueue>()
            .AddSingleton<InstalledPackagesStore>()
            // Settings (006)
            .AddSingleton<ISettingsStore, JsonSettingsStore>()
            .AddSingleton<ISettings, SettingsService>()
            // Platform glue
            .AddSingleton<DialogService>()
            .AddSingleton<IDialogService>(sp => sp.GetRequiredService<DialogService>())
            .AddSingleton<IClipboardService, ClipboardService>()
            .AddSingleton<ThemeService>()
            // View models: one per page, kept for the session (002 R4.2)
            .AddSingleton<PackageActions>()
            .AddSingleton<SearchViewModel>()
            .AddSingleton<InstalledViewModel>()
            .AddSingleton<UpdatesViewModel>()
            .AddSingleton<SettingsViewModel>()
            .AddSingleton<ShellViewModel>()
            .AddSingleton<ActivityViewModel>()
            .AddTransient<MainWindow>()
            .BuildServiceProvider();
}
