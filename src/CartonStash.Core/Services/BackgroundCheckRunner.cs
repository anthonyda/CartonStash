using CartonStash.Core.Models;

namespace CartonStash.Core.Services;

/// <summary>Shows the notification for a check (010 R2.1). A new one replaces the previous one (R2.3).</summary>
public interface INotifier
{
    void ShowUpdates(string title, string body);
}

/// <summary>What the open app is doing, if it's open (010 R1.5, R2.5).</summary>
public interface IAppActivity
{
    bool OperationsRunning { get; }

    bool WindowInForeground { get; }
}

public interface INetworkState
{
    bool IsMetered { get; }
}

/// <summary>Registers the schedule with Windows (010 R3.2).</summary>
public interface ICheckScheduler
{
    /// <summary>False in the portable app (R4.1).</summary>
    bool IsAvailable { get; }

    /// <summary>Replaces the schedule. Returns false when Windows doesn't allow background work (R3.4).</summary>
    Task<bool> ApplyAsync(AppSettings settings, DateTimeOffset? nextDue);

    /// <summary>The user or a policy turned the startup entry off, so a check missed while the PC was off waits for an unlock (R3.16).</summary>
    bool StartupBlocked { get; }
}

/// <summary>
/// One background check (010): decides whether it's due, asks WinGet, records the result and notifies. It reads only:
/// nothing here installs, updates or uninstalls (R1.4).
/// </summary>
public sealed class BackgroundCheckRunner(
    IPackageService packages,
    ISettings settings,
    CheckHistoryStore history,
    INotifier notifier,
    IAppActivity activity,
    INetworkState network,
    TimeProvider? clock = null,
    TimeZoneInfo? zone = null,
    ITaskbarBadge? badge = null)
{
    /// <summary>How long a refresh may take before the check uses the data WinGet already has (R1.9).</summary>
    public static readonly TimeSpan RefreshBudget = TimeSpan.FromSeconds(30);

    private const int NamesShown = 3;

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly TimeZoneInfo _zone = zone ?? TimeZoneInfo.Local;

    /// <summary>When the check after this moment is due, or null when checks are off.</summary>
    public DateTimeOffset? NextDue() => CheckSchedule.NextDue(settings.Current, Since(), _zone);

    /// <summary>Runs a check if one is due. Returns what it recorded, or null when none was due (R3.10). Never throws (R1.6).</summary>
    public async Task<CheckRecord?> RunAsync(CheckTrigger trigger, CancellationToken cancellationToken = default)
    {
        // BR-27: a check counts from when it started, so a slow or late one doesn't push the next timer tick out.
        var now = _clock.GetUtcNow();
        try
        {
            if (settings.WasReset)
            {
                return Record(new CheckRecord(now, trigger, CheckResult.Failed, Reason: "The app's settings couldn't be read."));
            }
            // BR-18: the due and skip tests are inside the try too, so an error there is recorded, never thrown.
            if (!CheckSchedule.IsDue(settings.Current, Since(), now, _zone, fromTimer: trigger == CheckTrigger.Schedule))
            {
                return null;
            }
            if (activity.OperationsRunning)
            {
                return Record(new CheckRecord(now, trigger, CheckResult.SkippedBusy));
            }
            if (network.IsMetered)
            {
                return Record(new CheckRecord(now, trigger, CheckResult.SkippedMetered));
            }

            await RefreshSourcesAsync(cancellationToken);
            var installed = await packages.GetInstalledAsync(cancellationToken);
            return Finish(installed, trigger, now);
        }
        catch (OperationCanceledException)
        {
            return TryRecord(new CheckRecord(now, trigger, CheckResult.Failed, Reason: "Windows stopped the check."));
        }
        catch (Exception ex)
        {
            return TryRecord(new CheckRecord(now, trigger, CheckResult.Failed, Reason: ex.Message));
        }
    }

    // R1.6: never throws, even when the history can't be written (R3.13).
    private CheckRecord? TryRecord(CheckRecord record)
    {
        try
        {
            return Record(record);
        }
        catch (Exception)
        {
            return record;
        }
    }

    /// <summary>"3 updates available, 1 ignored" (R2.1, R5.2).</summary>
    public static string NotificationTitle(int updates, int excluded)
    {
        var title = updates == 1 ? "1 update available" : $"{updates} updates available";
        return excluded > 0 ? $"{title}, {excluded} ignored" : title;
    }

    /// <summary>Up to three names, then "and 2 more" (R2.1).</summary>
    public static string NotificationBody(IReadOnlyList<string> names)
    {
        var shown = string.Join(", ", names.Take(NamesShown));
        return names.Count > NamesShown ? $"{shown} and {names.Count - NamesShown} more" : shown;
    }

    private DateTimeOffset Since() => CheckSchedule.Since(settings.Current, history.Read().FirstOrDefault(), _clock.GetUtcNow());

    private async Task RefreshSourcesAsync(CancellationToken cancellationToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(RefreshBudget);
        try
        {
            var sources = await packages.GetSourcesAsync(budget.Token);
            // Nobody is there to read an agreement, so a source that isn't accepted is left alone (006 R3.8).
            foreach (var source in sources.Where(s => !s.IsExplicit && s.AgreementStatus is AgreementStatus.None or AgreementStatus.Accepted))
            {
                await packages.RefreshSourceAsync(source.Name, budget.Token);
            }
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // R1.9 (BR-18): whatever stopped the refresh, go on with the data WinGet already has.
        }
    }

    private CheckRecord Finish(IReadOnlyList<PackageInfo> installed, CheckTrigger trigger, DateTimeOffset started)
    {
        // The Updates page's rule (R1.3).
        var withUpdate = installed.Where(p => p.HasUpdate && p.IsManaged).ToList();
        var names = withUpdate.Where(p => !settings.Current.IsExcluded(p.Id))
            .Select(p => p.Name)
            .Order(StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var excluded = withUpdate.Count - names.Count;

        var record = Record(new CheckRecord(
            started,
            trigger,
            names.Count > 0 ? CheckResult.Updates : CheckResult.None,
            names.Count,
            excluded,
            names));

        // BR-18: the check is recorded; a badge or notification that fails mustn't add a second, Failed entry.
        try
        {
            badge?.Show(names.Count); // 016 R2.1: only after a check that finished (R2.2)
        }
        catch (Exception)
        {
        }
        try
        {
            if (names.Count > 0 && !activity.WindowInForeground)
            {
                notifier.ShowUpdates(NotificationTitle(names.Count, excluded), NotificationBody(names));
            }
        }
        catch (Exception)
        {
        }
        return record;
    }

    private CheckRecord Record(CheckRecord record)
    {
        history.Append(record);
        return record;
    }
}
