using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using CartonStash.App.Services;
using CartonStash.Core.Services;

namespace CartonStash.App;

/// <summary>
/// Custom entry point (010 R1.2): when Windows starts the exe for a background check, it runs with no XAML at all.
/// Keep this small: it runs on every launch (007 R1.6).
/// </summary>
public static class Program
{
    private const string MainInstanceKey = "main";

    /// <summary>If Windows never hands over a check, or never finishes one, don't stay behind (R1.2).</summary>
    private static readonly TimeSpan BackgroundLimit = TimeSpan.FromMinutes(12);

    private static readonly ManualResetEvent BackgroundDone = new(false);

    /// <summary>The notification that opened this copy of the app, if any.</summary>
    public static bool StartedFromNotification { get; private set; }

    public static void SignalBackgroundDone() => BackgroundDone.Set();

    [STAThread]
    private static void Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        if (args.Contains("-RegisterForBGTaskServer"))
        {
            RunBackgroundServer();
            return;
        }
        if (AppPackage.IsPackaged && RedirectedToOpenApp())
        {
            return;
        }

        Application.Start(_ =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
            new App();
        });
    }

    private static void RunBackgroundServer()
    {
        var server = new Thread(() =>
        {
            BackgroundCheckTask.RegisterClass();
            BackgroundDone.WaitOne(BackgroundLimit);
        });
        server.SetApartmentState(ApartmentState.MTA);
        server.Start();
        server.Join();
    }

    // Only a notification click goes to the open copy (R2.2). A second launch from Start stays as it was.
    private static bool RedirectedToOpenApp()
    {
        var main = AppInstance.FindOrRegisterForKey(MainInstanceKey);
        var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
        StartedFromNotification = activation.Kind == ExtendedActivationKind.ToastNotification;
        if (main.IsCurrent || !StartedFromNotification)
        {
            return false;
        }

        Task.Run(() => main.RedirectActivationToAsync(activation).AsTask()).Wait();
        return true;
    }
}
