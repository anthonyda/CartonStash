using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CartonStash.Core.Models;
using CartonStash.Core.Services;

namespace CartonStash.Core.ViewModels;

/// <summary>One line of Check history (010 R3.11). <see cref="IsFailed"/> adds an icon, never color alone.</summary>
public sealed record CheckHistoryRow(string Text, bool IsFailed);

/// <summary>An excluded package, by name when it's installed and otherwise by its ID.</summary>
public sealed record ExcludedPackageRow(string Id, string Name)
{
    public string RemoveLabel => $"Remove {Name} from excluded packages";
}

/// <summary>Settings sections of spec 010: Update checks, Excluded packages, Check history.</summary>
public sealed partial class CheckSettingsViewModel : ObservableObject
{
    private readonly ISettings _settings;
    private readonly ICheckScheduler _scheduler;
    private readonly CheckHistoryStore _history;
    private readonly InstalledPackagesStore _installed;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _zone;
    private bool _loading = true;

    public CheckSettingsViewModel(ISettings settings, ICheckScheduler scheduler, CheckHistoryStore history, InstalledPackagesStore installed)
        : this(settings, scheduler, history, installed, TimeProvider.System, TimeZoneInfo.Local)
    {
    }

    internal CheckSettingsViewModel(ISettings settings, ICheckScheduler scheduler, CheckHistoryStore history, InstalledPackagesStore installed,
        TimeProvider clock, TimeZoneInfo zone)
    {
        _settings = settings;
        _scheduler = scheduler;
        _history = history;
        _installed = installed;
        _clock = clock;
        _zone = zone;

        var current = settings.Current;
        ModeIndex = (int)current.CheckMode;
        IntervalIndex = Math.Max(0, AppSettings.CheckIntervals.ToList().IndexOf(current.CheckIntervalHours));
        CheckTime = current.CheckTime.ToTimeSpan();
        _loading = false;

        _settings.Changed += (_, _) => Refresh();
        _installed.PropertyChanged += OnInstalledChanged;
        Refresh();
    }

    /// <summary>False in the portable app, which shows why (R4.1).</summary>
    public bool IsAvailable => _scheduler.IsAvailable;

    public bool IsUnavailable => !IsAvailable;

    /// <summary>RadioButtons index of <see cref="CheckMode"/>: Off, Interval, DailyTime (R3.1).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIntervalMode), nameof(IsDailyMode))]
    public partial int ModeIndex { get; set; }

    /// <summary>Only the chosen mode's control is available (R3.1).</summary>
    public bool IsIntervalMode => IsAvailable && ModeIndex == (int)CheckMode.Interval;

    public bool IsDailyMode => IsAvailable && ModeIndex == (int)CheckMode.DailyTime;

    /// <summary>ComboBox index into <see cref="AppSettings.CheckIntervals"/>.</summary>
    [ObservableProperty]
    public partial int IntervalIndex { get; set; }

    [ObservableProperty]
    public partial TimeSpan CheckTime { get; set; }

    /// <summary>Windows refused background work (R3.4). The chosen mode stays.</summary>
    [ObservableProperty]
    public partial bool IsBlocked { get; private set; }

    /// <summary>Checks are on but Windows won't start the app at sign-in (R3.16). A note: the schedule still works.</summary>
    [ObservableProperty]
    public partial bool IsStartupBlocked { get; private set; }

    [ObservableProperty]
    public partial string LastCheckText { get; private set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNextCheck))]
    public partial string NextCheckText { get; private set; } = "";

    public bool HasNextCheck => NextCheckText.Length > 0;

    public ObservableCollection<CheckHistoryRow> History { get; } = [];

    public bool HasNoHistory => History.Count == 0;

    public ObservableCollection<ExcludedPackageRow> Excluded { get; } = [];

    public bool HasNoExcluded => Excluded.Count == 0;

    /// <summary>The latest schedule change, for tests.</summary>
    internal Task LastApply { get; private set; } = Task.CompletedTask;

    /// <summary>Reloads what a background check or another page can change.</summary>
    public void Refresh()
    {
        var records = _history.Read();
        var now = _clock.GetUtcNow();

        History.Clear();
        foreach (var record in records)
        {
            History.Add(new CheckHistoryRow(CheckText.Describe(record, now, _zone), record.Result == CheckResult.Failed));
        }
        OnPropertyChanged(nameof(HasNoHistory));

        LastCheckText = records.Count > 0 ? $"Last check: {CheckText.Describe(records[0], now, _zone)}" : "No checks yet.";
        var since = CheckSchedule.Since(_settings.Current, records.FirstOrDefault(), now);
        NextCheckText = CheckSchedule.NextDue(_settings.Current, since, _zone) is { } next
            ? $"Next check: {CheckText.When(next, now, _zone)} (approximate)"
            : "";
        ReadStartupBlocked();

        Excluded.Clear();
        foreach (var id in _settings.Current.ExcludedPackageIds)
        {
            var package = _installed.Packages.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
            Excluded.Add(new ExcludedPackageRow(id, package?.Name ?? id));
        }
        OnPropertyChanged(nameof(HasNoExcluded));
    }

    /// <summary>Applies from the next check (R5.4).</summary>
    [RelayCommand]
    private void RemoveExclusion(ExcludedPackageRow row) => _settings.Update(s => s.WithoutExcluded(row.Id));

    partial void OnModeIndexChanged(int value) => ScheduleChanged(s => s with { CheckMode = (CheckMode)value });

    partial void OnIntervalIndexChanged(int value)
    {
        if (value >= 0 && value < AppSettings.CheckIntervals.Count)
        {
            ScheduleChanged(s => s with { CheckIntervalHours = AppSettings.CheckIntervals[value] });
        }
    }

    partial void OnCheckTimeChanged(TimeSpan value) => ScheduleChanged(s => s with { CheckTime = TimeOnly.FromTimeSpan(value) });

    // R3.2: replaces the schedule at once.
    private void ScheduleChanged(Func<AppSettings, AppSettings> change)
    {
        if (_loading)
        {
            return;
        }
        _settings.Update(s => change(s) with { CheckScheduleSetAt = _clock.GetUtcNow() });
        LastApply = ApplyAsync();
    }

    private async Task ApplyAsync()
    {
        var current = _settings.Current;
        var since = CheckSchedule.Since(current, _history.Read().FirstOrDefault(), _clock.GetUtcNow());
        try
        {
            IsBlocked = !await _scheduler.ApplyAsync(current, CheckSchedule.NextDue(current, since, _zone));
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or UnauthorizedAccessException or InvalidOperationException)
        {
            IsBlocked = true;
        }
        ReadStartupBlocked();
    }

    private void ReadStartupBlocked() =>
        IsStartupBlocked = IsAvailable && _settings.Current.CheckMode != CheckMode.Off && _scheduler.StartupBlocked;

    private void OnInstalledChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(InstalledPackagesStore.Packages))
        {
            Refresh();
        }
    }
}

/// <summary>Plain words for a check (010 R3.11): "Today 10:03 PM · Schedule · 3 updates, 1 excluded · Git, Node.js".</summary>
public static class CheckText
{
    public static string Describe(CheckRecord record, DateTimeOffset now, TimeZoneInfo zone)
    {
        var trigger = record.Trigger == CheckTrigger.MissedCheck ? "Missed check" : "Schedule";
        var parts = new List<string> { When(record.Time, now, zone), trigger, Result(record) };
        if (record.Names is { Count: > 0 } names)
        {
            parts.Add(string.Join(", ", names));
        }
        return string.Join(" · ", parts);
    }

    public static string When(DateTimeOffset time, DateTimeOffset now, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTime(time, zone);
        var days = (DateOnly.FromDateTime(local.DateTime).DayNumber - DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime).DayNumber);
        var day = days switch
        {
            0 => "Today",
            -1 => "Yesterday",
            1 => "Tomorrow",
            _ => local.DateTime.ToShortDateString(),
        };
        return $"{day} {local.DateTime.ToShortTimeString()}";
    }

    private static string Result(CheckRecord record) => record.Result switch
    {
        CheckResult.Updates => Excluded(record.UpdateCount == 1 ? "1 update" : $"{record.UpdateCount} updates", record),
        CheckResult.None => Excluded("No updates", record),
        CheckResult.SkippedBusy => "Skipped: operations were running",
        CheckResult.SkippedMetered => "Skipped: metered connection",
        _ => $"Failed: {record.Reason ?? "unknown reason"}",
    };

    private static string Excluded(string text, CheckRecord record) => record.ExcludedCount > 0 ? $"{text}, {record.ExcludedCount} excluded" : text; // R5.8
}
