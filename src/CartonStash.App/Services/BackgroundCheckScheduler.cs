using Windows.ApplicationModel;
using Windows.ApplicationModel.Background;
using CartonStash.Core.Models;
using CartonStash.Core.Services;
using BackgroundTaskBuilder = Microsoft.Windows.ApplicationModel.Background.BackgroundTaskBuilder;

namespace CartonStash.App.Services;

/// <summary>Registers the chosen schedule with Windows (010 R3.2). MSIX only: the portable app has no background checks (R4.1).</summary>
public sealed class BackgroundCheckScheduler : ICheckScheduler
{
    public bool IsAvailable => AppPackage.IsPackaged;

    public bool StartupBlocked { get; private set; }

    public const string TimerTaskName = "CartonStash check timer";

    /// <summary>Runs when the user comes back, to catch up on a missed check (R3.10). Windows' UserPresent trigger (T1).</summary>
    public const string ReturnTaskName = "CartonStash check on return";

    /// <summary>The manifest's startup entry (R3.14). It isn't a background task, so <see cref="RemoveAll"/> leaves it.</summary>
    public const string StartupTaskId = "CartonStashCheck";

    /// <summary>Replaces every task with the chosen schedule. Returns false when Windows doesn't allow background work (R3.4).</summary>
    public async Task<bool> ApplyAsync(AppSettings settings, DateTimeOffset? nextDue)
    {
        // One at a time, in order: two changes that overlapped both registered their tasks (T16: two "check on return").
        await _applying.WaitAsync();
        try
        {
            return await ApplyOnceAsync(settings, nextDue);
        }
        finally
        {
            _applying.Release();
        }
    }

    private readonly SemaphoreSlim _applying = new(1, 1);

    private async Task<bool> ApplyOnceAsync(AppSettings settings, DateTimeOffset? nextDue)
    {
        await ApplyStartupAsync(settings);
        var access = settings.CheckMode == CheckMode.Off ? BackgroundAccessStatus.Unspecified : await BackgroundExecutionManager.RequestAccessAsync();

        RemoveAll();
        if (settings.CheckMode == CheckMode.Off)
        {
            return true;
        }
        if (access is BackgroundAccessStatus.DeniedByUser or BackgroundAccessStatus.DeniedBySystemPolicy or BackgroundAccessStatus.Unspecified)
        {
            return false;
        }

        if (settings.CheckMode == CheckMode.Interval)
        {
            Register(TimerTaskName, new TimeTrigger((uint)settings.CheckIntervalHours * 60, oneShot: false));
        }
        else if (nextDue is { } next)
        {
            RegisterDailyTimer(next);
        }
        Register(ReturnTaskName, new SystemTrigger(SystemTriggerType.UserPresent, oneShot: false));
        return true;
    }

    /// <summary>
    /// The startup entry follows checks on or off (R3.15). Also run when the app opens, so an update from a version without
    /// the entry turns it on. Windows doesn't let an app turn back on what the user or a policy turned off: that's reported (R3.16).
    /// </summary>
    public async Task ApplyStartupAsync(AppSettings settings)
    {
        try
        {
            var entry = await StartupTask.GetAsync(StartupTaskId);
            var on = settings.CheckMode != CheckMode.Off;
            var state = entry.State;
            if (on && state == StartupTaskState.Disabled)
            {
                // Windows starts the exe through the entry at once; no check is due then, so it exits (design, T13).
                state = await entry.RequestEnableAsync();
            }
            else if (!on && state == StartupTaskState.Enabled)
            {
                entry.Disable();
            }
            StartupBlocked = on && state is StartupTaskState.DisabledByUser or StartupTaskState.DisabledByPolicy;
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or ArgumentException or UnauthorizedAccessException)
        {
            // The schedule still works without the entry: a missed check waits for an unlock.
        }
    }

    /// <summary>True when the chosen schedule has lost a task: repairs a broken daily chain (R3.7).</summary>
    public static bool IsMissing(AppSettings settings)
    {
        if (settings.CheckMode == CheckMode.Off)
        {
            return false;
        }
        var names = BackgroundTaskRegistration.AllTasks.Values.Select(t => t.Name).ToList();
        return !names.Contains(TimerTaskName) || !names.Contains(ReturnTaskName);
    }

    public static void RegisterDailyTimer(DateTimeOffset next)
    {
        Remove(TimerTaskName);
        Register(TimerTaskName, new TimeTrigger(CheckSchedule.TimerMinutes(next, DateTimeOffset.Now), oneShot: true));
    }

    private static void Register(string name, IBackgroundTrigger trigger)
    {
        var builder = new BackgroundTaskBuilder { Name = name };
        builder.SetTrigger(trigger);
        builder.SetTaskEntryPointClsid(typeof(BackgroundCheckTask).GUID);
        builder.Register();
    }

    private static void RemoveAll()
    {
        foreach (var task in BackgroundTaskRegistration.AllTasks.Values.ToList())
        {
            task.Unregister(true);
        }
    }

    private static void Remove(string name)
    {
        foreach (var task in BackgroundTaskRegistration.AllTasks.Values.Where(t => t.Name == name).ToList())
        {
            task.Unregister(false);
        }
    }
}
