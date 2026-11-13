using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using CartonStash.App.Services;
using CartonStash.Core.Models;
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

    /// <summary>Set when this copy was started to finish App Installer's update (004 R6.3).</summary>
    public static RestartArguments? Restart { get; private set; }

    /// <summary>The notification that opened this copy of the app, if any.</summary>
    public static bool StartedFromNotification { get; private set; }

    // 010 R3.18: the timer and "check on return" tasks share this process; it ends when the last one is done.
    internal static readonly ActiveTasks BackgroundTasks = new();

    public static void SignalBackgroundDone() => BackgroundDone.Set();

    [STAThread]
    private static void Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        // 017 R1.4: a copy started with administrator rights for one operation. No window; it ends with the operation.
        if (args.Length == 3 && args[0] == ElevatedProtocol.Argument)
        {
            Environment.ExitCode = ElevatedProcessRunner.RunHere(args[1], args[2]);
            return;
        }

        if (args.Contains("-RegisterForBGTaskServer"))
        {
            RunBackgroundServer();
            return;
        }
        Restart = RestartArguments.Parse(args);
        if (Restart is not null)
        {
            // 004 R6.3: the old copy is ending; it must be gone before this one takes the "main" key (010 R2.2).
            AppRestarter.WaitForExit([Restart.AppProcessId], TimeSpan.FromSeconds(10));
        }
        if (AppPackage.IsPackaged)
        {
            var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
            if (activation.Kind == ExtendedActivationKind.StartupTask)
            {
                RunStartupCheck();
                return;
            }
            if (RedirectedToOpenApp(activation))
            {
                return;
            }
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
            if (BackgroundCheckTask.RegisterClass()) // BR-33: if Windows refused, no task comes: exit now, not after the time limit
            {
                BackgroundDone.WaitOne(BackgroundLimit);
            }
        });
        server.SetApartmentState(ApartmentState.MTA);
        server.Start();
        server.Join();
    }

    // R3.14: Windows started the exe at sign-in through the startup entry. No window, and never the "main" instance key.
    // A missed check runs if one is due; otherwise the runner records nothing and the process exits at once.
    private static void RunStartupCheck()
    {
        using var limit = new CancellationTokenSource(BackgroundLimit);
        try
        {
            Task.Run(() => BackgroundCheckTask.CheckAsync(CheckTrigger.MissedCheck, limit.Token)).Wait(BackgroundLimit);
        }
        catch (AggregateException)
        {
            // R1.6: a check never crashes. The runner records its own failures; this covers setup.
        }
    }

    // Only a notification click goes to the open copy (R2.2). A second launch from Start stays as it was.
    private static bool RedirectedToOpenApp(AppActivationArguments activation)
    {
        var main = AppInstance.FindOrRegisterForKey(MainInstanceKey);
        StartedFromNotification = activation.Kind == ExtendedActivationKind.ToastNotification;
        if (main.IsCurrent || !StartedFromNotification)
        {
            return false;
        }

        Foreground.AllowFor(main.ProcessId); // the click's right to the foreground goes to the open window (018 R1.4)
        Task.Run(() => main.RedirectActivationToAsync(activation).AsTask()).Wait();
        return true;
    }
}
