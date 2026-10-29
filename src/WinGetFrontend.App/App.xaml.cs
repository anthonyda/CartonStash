using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using WinGetFrontend.Core.Services;
using WinGetFrontend.Core.ViewModels;

namespace WinGetFrontend.App;

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
        _window = Services.GetRequiredService<MainWindow>();
        _window.Activate();
    }

    private static ServiceProvider ConfigureServices() =>
        new ServiceCollection()
            .AddSingleton<IPackageService, WinGetPackageService>()
            .AddTransient<SearchViewModel>()
            .AddTransient<MainWindow>()
            .BuildServiceProvider();
}
