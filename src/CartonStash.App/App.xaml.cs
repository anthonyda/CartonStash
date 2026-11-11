using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using CartonStash.App.Services;
using CartonStash.Core;
using CartonStash.Core.Services;
using CartonStash.Core.ViewModels;

namespace CartonStash.App;

public partial class App : Application
{
    /// <summary>Margin after WinGet's old server stops, before the new copy connects (004 R6.3, T15).</summary>
    private static readonly TimeSpan SwitchMarkWait = TimeSpan.FromSeconds(2);

    private Window? _window;
    private FileSystemWatcher? _historyWatcher;

    public static IServiceProvider Services { get; } = ConfigureServices();

    public App()
    {
        InitializeComponent();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (Program.Restart is { } restart)
        {
            // 004 R6.3: WinGet's old server stops about 10 s after the old copy (T8). Connecting before that would bring
            // back the old App Installer. Nothing here touches WinGet.
            _window = new FinishUpdateWindow();
            Services.GetRequiredService<ThemeService>().Attach(_window);
            _window.Activate();
            await Task.Run(() => AppRestarter.WaitForExit(restart.ServerProcessIds, TimeSpan.FromSeconds(60)));
            // About 0.1 s after the server stops, Windows marks the staged version to take over at the next start. A COM
            // connection made before that starts the old version again (T15 finding).
            await Task.Delay(SwitchMarkWait);
        }

        // Before anything resolves: activating WinGet when it's missing ends the process (011 R3.1).
        var installed = WinGetAvailability.IsInstalled();
        if (!installed || !TryOpenMainWindow())
        {
            ShowSetup(startFailed: installed);
        }
    }

    private bool TryOpenMainWindow()
    {
        try
        {
            Services.GetRequiredService<CatalogConnection>(); // activates WinGet (011 R3.5)
        }
        catch (COMException)
        {
            return false;
        }

        var setup = _window;
        // Resolved on the UI thread: the queue and store rely on its SynchronizationContext.
        var window = Services.GetRequiredService<MainWindow>();
        _window = window;
        if (AppPackage.IsPackaged)
        {
            ConnectBackgroundChecks(window);
        }
        Services.GetRequiredService<AppRestarter>().CurrentPage = () => window.CurrentPageKey;
        if (Program.Restart is { } restart)
        {
            window.OpenPage(restart.Page);
            _ = Services.GetRequiredService<ShellViewModel>().FinishRestartAsync(restart.PendingVersion); // R6.4
        }
        _window.Activate();
        setup?.Close();
        return true;
    }

    private void ShowSetup(bool startFailed)
    {
        var previous = _window; // the restart's waiting window, if any
        _window = new WinGetSetupWindow(new WinGetSetupViewModel(startFailed, WinGetAvailability.IsInstalled, TryOpenMainWindow, WinGetAvailability.SetUpAsync));
        Services.GetRequiredService<ThemeService>().Attach(_window);
        _window.Activate();
        previous?.Close();
    }

    // Spec 010, MSIX only (R4.1).
    private void ConnectBackgroundChecks(MainWindow window)
    {
        var marker = Services.GetRequiredService<AppActivityMarker>();
        var queue = Services.GetRequiredService<PackageOperationQueue>();
        queue.Enqueued += (_, _) => marker.SetOperationsRunning(true);
        queue.Drained += (_, _) => marker.SetOperationsRunning(false);
        window.Activated += (_, e) => marker.SetWindowInForeground(e.WindowActivationState != WindowActivationState.Deactivated);
        window.Closed += (_, _) => marker.SetWindowInForeground(false);

        // R2.2: a notification clicked while the app is open comes here from Program.
        AppInstance.GetCurrent().Activated += (_, e) =>
        {
            if (e.Kind == ExtendedActivationKind.ToastNotification)
            {
                window.DispatcherQueue.TryEnqueue(() =>
                {
                    window.ShowUpdates();
                    window.Activate();
                });
            }
        };
        if (Program.StartedFromNotification)
        {
            window.ShowUpdates();
        }

        // R1.7: a check that finishes while the app is open updates the badge and Settings.
        var history = Services.GetRequiredService<CheckHistoryStore>();
        var watcher = new FileSystemWatcher(Path.GetDirectoryName(history.FilePath)!, Path.GetFileName(history.FilePath)) { EnableRaisingEvents = true };
        watcher.Renamed += (_, _) => window.DispatcherQueue.TryEnqueue(() =>
        {
            Services.GetRequiredService<CheckSettingsViewModel>().Refresh();
            _ = Services.GetRequiredService<InstalledPackagesStore>().ReloadAsync();
        });
        _historyWatcher = watcher;

        var settings = Services.GetRequiredService<ISettings>();
        if (BackgroundCheckScheduler.IsMissing(settings.Current))
        {
            _ = RepairScheduleAsync(settings);
        }
        else
        {
            _ = Services.GetRequiredService<BackgroundCheckScheduler>().ApplyStartupAsync(settings.Current); // R3.15
        }
    }

    // R3.7: repairs a broken daily chain. If Windows refuses, Settings says so (R3.4), and the app carries on.
    private static async Task RepairScheduleAsync(ISettings settings)
    {
        try
        {
            var last = Services.GetRequiredService<CheckHistoryStore>().Read().FirstOrDefault();
            var since = CheckSchedule.Since(settings.Current, last, DateTimeOffset.Now);
            await Services.GetRequiredService<BackgroundCheckScheduler>()
                .ApplyAsync(settings.Current, CheckSchedule.NextDue(settings.Current, since, TimeZoneInfo.Local));
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or UnauthorizedAccessException)
        {
        }
    }

    private static ServiceProvider ConfigureServices() =>
        new ServiceCollection()
            // WinGet (Core/Services is the only layer that touches it)
            .AddSingleton<CatalogConnection>()
            .AddSingleton<IPackageService, WinGetPackageService>()
            .AddSingleton<IPackageInstaller, WinGetPackageInstaller>()
            .AddSingleton<IInstallContext, MsiInstallContext>() // 014 R3.8
            .AddSingleton<IRunningPrograms, RunningPrograms>() // 003 R7.2
            // GitHub release notes, only when the user asks (012 R2.2)
            .AddSingleton<IReleaseNotesService>(_ => new GitHubReleaseNotesService(AppInfo.UserAgent))
            .AddSingleton<IWinGetRecordReader, WinGetRecordReader>()
            .AddSingleton<IOperationLogStore, OperationLogStore>()
            .AddSingleton<PackageOperationQueue>()
            .AddSingleton<InstalledPackagesStore>()
            .AddSingleton<SourceAgreementCheck>()
            .AddSingleton<WinGetHost>()
            // Settings (006)
            .AddSingleton<ISettingsStore, JsonSettingsStore>()
            .AddSingleton<ISettings, SettingsService>()
            // Platform glue
            .AddSingleton<DialogService>()
            .AddSingleton<IDialogService>(sp => sp.GetRequiredService<DialogService>())
            .AddSingleton<IClipboardService, ClipboardService>()
            .AddSingleton<ThemeService>()
            .AddSingleton<AppActivityMarker>()
            .AddSingleton<AppRestarter>()
            .AddSingleton<IAppRestarter>(sp => sp.GetRequiredService<AppRestarter>())
            .AddSingleton<BackgroundCheckScheduler>()
            .AddSingleton<ICheckScheduler>(sp => sp.GetRequiredService<BackgroundCheckScheduler>())
            .AddSingleton(_ => new CheckHistoryStore(AppPackage.LocalFolder
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppInfo.DataFolder)))
            // View models: one per page, kept for the session (002 R4.2)
            .AddSingleton<PackageActions>()
            .AddSingleton<SearchViewModel>()
            .AddSingleton<InstalledViewModel>()
            .AddSingleton<UpdatesViewModel>()
            .AddSingleton<SettingsViewModel>()
            .AddSingleton<CheckSettingsViewModel>()
            .AddSingleton<ShellViewModel>()
            .AddSingleton<ActivityViewModel>()
            .AddTransient<MainWindow>()
            .BuildServiceProvider();
}
