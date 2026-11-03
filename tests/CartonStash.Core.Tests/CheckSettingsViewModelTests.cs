using CartonStash.Core.Models;
using CartonStash.Core.Services;
using CartonStash.Core.ViewModels;

namespace CartonStash.Core.Tests;

/// <summary>Spec 010 R3, R4, R5.4.</summary>
public sealed class CheckSettingsViewModelTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "CartonStashTests", Guid.NewGuid().ToString("N"));
    private readonly FakeScheduler _scheduler = new();
    private readonly TestApp _app = new();
    private readonly CheckHistoryStore _history;

    public CheckSettingsViewModelTests() => _history = new CheckHistoryStore(_dir, new FixedClock());

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private CheckSettingsViewModel Create(ISettings? settings = null) =>
        new(settings ?? _app.Settings, _scheduler, _history, _app.Store, new FixedClock(), TimeZoneInfo.Utc);

    [Fact]
    public void Opens_on_the_saved_choices_and_applies_nothing() // R3.1
    {
        var settings = new FakeSettings(new AppSettings { CheckMode = CheckMode.Interval, CheckIntervalHours = 168, CheckTime = new TimeOnly(6, 30) });

        var vm = Create(settings);

        Assert.Equal(1, vm.ModeIndex);
        Assert.Equal(4, vm.IntervalIndex);
        Assert.Equal(TimeSpan.FromHours(6.5), vm.CheckTime);
        Assert.Empty(_scheduler.Applied);
    }

    [Fact]
    public void Only_the_chosen_modes_control_is_available() // R3.1
    {
        var vm = Create();
        Assert.False(vm.IsIntervalMode || vm.IsDailyMode);

        vm.ModeIndex = (int)CheckMode.Interval;
        Assert.True(vm.IsIntervalMode);
        Assert.False(vm.IsDailyMode);

        vm.ModeIndex = (int)CheckMode.DailyTime;
        Assert.False(vm.IsIntervalMode);
        Assert.True(vm.IsDailyMode);
    }

    [Fact]
    public async Task Changing_the_mode_saves_and_replaces_the_schedule_at_once() // R3.2
    {
        var vm = Create();

        vm.ModeIndex = (int)CheckMode.DailyTime;
        await vm.LastApply;

        Assert.Equal(CheckMode.DailyTime, _app.Settings.Current.CheckMode);
        Assert.Equal(Now, _app.Settings.Current.CheckScheduleSetAt);
        var (settings, next) = Assert.Single(_scheduler.Applied);
        Assert.Equal(CheckMode.DailyTime, settings.CheckMode);
        Assert.Equal(new DateTimeOffset(2026, 6, 15, 22, 0, 0, TimeSpan.Zero), next);
    }

    [Fact]
    public async Task Changing_the_interval_or_time_replaces_the_schedule() // R3.2
    {
        var vm = Create();

        vm.IntervalIndex = 1;
        vm.CheckTime = TimeSpan.FromHours(7);
        await vm.LastApply;

        Assert.Equal(2, _scheduler.Applied.Count);
        Assert.Equal(6, _app.Settings.Current.CheckIntervalHours);
        Assert.Equal(new TimeOnly(7, 0), _app.Settings.Current.CheckTime);
    }

    [Fact]
    public async Task Windows_refusing_keeps_the_choice_and_says_so() // R3.4
    {
        _scheduler.Allowed = false;
        var vm = Create();

        vm.ModeIndex = (int)CheckMode.Interval;
        await vm.LastApply;

        Assert.True(vm.IsBlocked);
        Assert.Equal(CheckMode.Interval, _app.Settings.Current.CheckMode);
    }

    [Fact]
    public void Startup_entry_turned_off_shows_the_note_when_settings_open() // R3.16
    {
        _scheduler.StartupBlocked = true;

        var vm = Create(new FakeSettings(new AppSettings { CheckMode = CheckMode.Interval }));

        Assert.True(vm.IsStartupBlocked);
        Assert.False(vm.IsBlocked);
    }

    [Fact]
    public async Task Startup_entry_note_follows_the_schedule_change() // R3.16
    {
        var vm = Create();
        Assert.False(vm.IsStartupBlocked);

        _scheduler.StartupBlocked = true;
        vm.ModeIndex = (int)CheckMode.DailyTime;
        await vm.LastApply;
        Assert.True(vm.IsStartupBlocked);

        vm.ModeIndex = (int)CheckMode.Off;
        await vm.LastApply;
        Assert.False(vm.IsStartupBlocked);
    }

    [Fact]
    public void Startup_entry_note_is_hidden_in_the_portable_app() // R3.16, R4.1
    {
        _scheduler.IsAvailable = false;
        _scheduler.StartupBlocked = true;

        var vm = Create(new FakeSettings(new AppSettings { CheckMode = CheckMode.Interval }));

        Assert.False(vm.IsStartupBlocked);
    }

    [Fact]
    public void Portable_app_says_it_is_unavailable_and_keeps_controls_off() // R4.1
    {
        _scheduler.IsAvailable = false;
        var settings = new FakeSettings(new AppSettings { CheckMode = CheckMode.Interval });

        var vm = Create(settings);

        Assert.True(vm.IsUnavailable);
        Assert.False(vm.IsIntervalMode);
    }

    [Fact]
    public void History_is_listed_newest_first_with_the_last_and_next_check() // R3.3, R3.11
    {
        var settings = new FakeSettings(new AppSettings { CheckMode = CheckMode.Interval, CheckIntervalHours = 6, CheckScheduleSetAt = Now.AddDays(-1) });
        _history.Append(new CheckRecord(Now.AddHours(-30), CheckTrigger.Schedule, CheckResult.None));
        _history.Append(new CheckRecord(Now.AddHours(-1), CheckTrigger.MissedCheck, CheckResult.Failed, Reason: "No network"));

        var vm = Create(settings);

        Assert.Equal(2, vm.History.Count);
        Assert.True(vm.History[0].IsFailed);
        Assert.Contains("Missed check · Failed: No network", vm.History[0].Text);
        Assert.Contains("No network", vm.LastCheckText);
        Assert.StartsWith("Next check: Today", vm.NextCheckText);
        Assert.False(vm.HasNoHistory);
    }

    [Fact]
    public void No_history_says_so()
    {
        var vm = Create();

        Assert.True(vm.HasNoHistory);
        Assert.Equal("No checks yet.", vm.LastCheckText);
        Assert.Equal("", vm.NextCheckText);
    }

    [Fact]
    public async Task Excluded_list_shows_names_and_falls_back_to_the_id() // design, edge cases
    {
        _app.SetInstalled(Pkg.Installed("Git.Git", "Git"));
        await _app.Store.EnsureLoadedAsync();
        var settings = new FakeSettings(new AppSettings { ExcludedPackageIds = ["Git.Git", "Gone.App"] });

        var vm = Create(settings);

        Assert.Equal(["Git", "Gone.App"], vm.Excluded.Select(r => r.Name));
    }

    [Fact]
    public void Removing_an_exclusion_applies_and_leaves_the_list() // R5.4
    {
        var settings = new FakeSettings(new AppSettings { ExcludedPackageIds = ["A"] });
        var vm = Create(settings);

        vm.RemoveExclusionCommand.Execute(vm.Excluded[0]);

        Assert.Empty(settings.Current.ExcludedPackageIds);
        Assert.True(vm.HasNoExcluded);
    }

    [Theory]
    [InlineData(CheckResult.Updates, 3, 1, "3 updates, 1 excluded · Git")]
    [InlineData(CheckResult.Updates, 1, 0, "1 update · Git")]
    [InlineData(CheckResult.None, 0, 2, "No updates, 2 excluded")]
    [InlineData(CheckResult.SkippedBusy, 0, 0, "Skipped: operations were running")]
    [InlineData(CheckResult.SkippedMetered, 0, 0, "Skipped: metered connection")]
    public void Each_result_reads_in_plain_words(CheckResult result, int updates, int excluded, string expected) // R3.11, R5.8
    {
        var names = updates > 0 ? new[] { "Git" } : null;

        var text = CheckText.Describe(new CheckRecord(Now, CheckTrigger.Schedule, result, updates, excluded, names), Now, TimeZoneInfo.Utc);

        Assert.EndsWith($"· Schedule · {expected}", text);
        Assert.StartsWith("Today", text);
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FakeScheduler : ICheckScheduler
    {
        public bool IsAvailable { get; set; } = true;

        public bool Allowed { get; set; } = true;

        public bool StartupBlocked { get; set; }

        public List<(AppSettings Settings, DateTimeOffset? Next)> Applied { get; } = [];

        public Task<bool> ApplyAsync(AppSettings settings, DateTimeOffset? nextDue)
        {
            Applied.Add((settings, nextDue));
            return Task.FromResult(Allowed);
        }
    }
}
