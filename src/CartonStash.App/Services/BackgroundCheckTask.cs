using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Windows.ApplicationModel.Background;
using CartonStash.Core.Models;
using CartonStash.Core.Services;
using WinRT;

namespace CartonStash.App.Services;

/// <summary>
/// The class Windows activates for a background check (010 R1.1). All decisions are in <see cref="BackgroundCheckRunner"/>;
/// this only connects it to Windows, so it has no XAML and no service container.
/// </summary>
[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
[Guid("7C5B3E1A-6F42-4D0B-9A17-2E8F4C1D5A10")]
[ComSourceInterfaces(typeof(IBackgroundTask))]
public sealed class BackgroundCheckTask : IBackgroundTask
{
    private const string RunningMutex = @"Local\CartonStash.CheckRunning";

    private readonly CancellationTokenSource _cancelled = new();

    public static void RegisterClass()
    {
        var id = typeof(BackgroundCheckTask).GUID;
        ComServer.CoRegisterClassObject(ref id, new ComServer.Factory(), ComServer.ClsctxLocalServer, ComServer.RegclsMultipleUse, out _);
    }

    [MTAThread]
    public void Run(IBackgroundTaskInstance taskInstance)
    {
        var deferral = taskInstance.GetDeferral();
        taskInstance.Canceled += (_, _) => _cancelled.Cancel();
        var name = taskInstance.Task.Name;

        _ = Task.Run(async () =>
        {
            try
            {
                await CheckAsync(name == BackgroundCheckScheduler.ReturnTaskName ? CheckTrigger.MissedCheck : CheckTrigger.Schedule, _cancelled.Token);
            }
            catch (Exception)
            {
                // R1.6: a check never crashes. The runner records its own failures; this covers setup.
            }
            deferral.Complete();
            Program.SignalBackgroundDone();
        });
    }

    /// <summary>One check, from a background task or the startup entry (R3.14). Same runner, history and notification.</summary>
    public static async Task CheckAsync(CheckTrigger trigger, CancellationToken cancel)
    {
        // A second trigger at the same moment, such as the startup entry and UserPresent at sign-in, exits and records nothing (R3.17).
        using var running = new Mutex(true, RunningMutex, out var isOnlyCheck);
        if (!isOnlyCheck)
        {
            return;
        }

        var settings = new SettingsService(new JsonSettingsStore());
        var runner = new BackgroundCheckRunner(
            new WinGetPackageService(new CatalogConnection(settings)), // leaves out sources not accepted (006 R3.8)
            settings,
            new CheckHistoryStore(AppPackage.LocalFolder!),
            new WindowsNotifier(),
            new AppActivityMarker(),
            new WindowsNetworkState(),
            badge: new WindowsTaskbarBadge()); // 016 R2.1

        await runner.RunAsync(trigger, cancel);

        // A daily timer fires once, and possibly early, so each run sets the next one.
        if (settings.Current.CheckMode == CheckMode.DailyTime && runner.NextDue() is { } next)
        {
            BackgroundCheckScheduler.RegisterDailyTimer(next);
        }
    }
}

internal static partial class ComServer
{
    public const uint ClsctxLocalServer = 4;
    public const uint RegclsMultipleUse = 1;

    private const uint SOk = 0;
    private const uint ClassENoAggregation = 0x80040110;
    private const uint ENoInterface = 0x80004002;
    private const string IidIUnknown = "00000000-0000-0000-C000-000000000046";
    private const string IidIClassFactory = "00000001-0000-0000-C000-000000000046";

    [LibraryImport("ole32.dll")]
    public static partial int CoRegisterClassObject(ref Guid classId, [MarshalAs(UnmanagedType.Interface)] IClassFactory objectAsUnknown, uint executionContext, uint flags, out uint registrationToken);

    [GeneratedComInterface]
    [Guid(IidIClassFactory)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public partial interface IClassFactory
    {
        [PreserveSig]
        uint CreateInstance(IntPtr objectAsUnknown, in Guid interfaceId, out IntPtr objectPointer);

        [PreserveSig]
        uint LockServer([MarshalAs(UnmanagedType.Bool)] bool Lock);
    }

    [GeneratedComClass]
    internal sealed partial class Factory : IClassFactory
    {
        public uint CreateInstance(IntPtr objectAsUnknown, in Guid interfaceId, out IntPtr objectPointer)
        {
            objectPointer = IntPtr.Zero;
            if (objectAsUnknown != IntPtr.Zero)
            {
                return ClassENoAggregation;
            }
            if (interfaceId != typeof(BackgroundCheckTask).GUID && interfaceId != new Guid(IidIUnknown))
            {
                return ENoInterface;
            }
            objectPointer = MarshalInterface<IBackgroundTask>.FromManaged(new BackgroundCheckTask());
            return SOk;
        }

        public uint LockServer(bool Lock) => SOk;
    }
}
