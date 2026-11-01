using CartonStash.Core.Models;
using CartonStash.Core.Services;

namespace CartonStash.Core.Tests;

/// <summary>Spec 010.</summary>
public sealed class BackgroundCheckRunnerTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "CartonStashTests", Guid.NewGuid().ToString("N"));
    private readonly FakePackageService _packages = new();
    private readonly FakeSettings _settings = new()
    {
        Current = new AppSettings { CheckMode = CheckMode.Interval, CheckIntervalHours = 6, CheckScheduleSetAt = Now.AddHours(-7) },
    };
    private readonly FakeNotifier _notifier = new();
    private readonly FakeActivity _activity = new();
    private readonly FakeNetwork _network = new();
    private readonly CheckHistoryStore _history;
    private readonly BackgroundCheckRunner _runner;

    public BackgroundCheckRunnerTests()
    {
        var clock = new FixedClock();
        _history = new CheckHistoryStore(_dir, clock);
        _runner = new BackgroundCheckRunner(_packages, _settings, _history, _notifier, _activity, _network, clock, TimeZoneInfo.Utc);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private void Installed(params PackageInfo[] packages) =>
        _packages.Installed = _ => Task.FromResult<IReadOnlyList<PackageInfo>>(packages);

    private static SourceInfo Source(string name, bool isExplicit = false) => new(name, "", "", false, isExplicit, null, []);

    private Task<CheckRecord?> Run() => _runner.RunAsync(CheckTrigger.Schedule);

    [Fact]
    public async Task Not_due_does_nothing_and_records_nothing() // R3.10
    {
        _settings.Current = _settings.Current with { CheckScheduleSetAt = Now.AddHours(-1) };

        Assert.Null(await Run());

        Assert.Equal(0, _packages.InstalledCalls);
        Assert.Empty(_history.Read());
    }

    [Fact]
    public async Task Off_never_runs()
    {
        _settings.Current = new AppSettings();

        Assert.Null(await Run());
    }

    [Fact]
    public async Task A_check_after_one_just_ran_is_not_due() // T1: triggers fired 10 s apart
    {
        await Run();

        Assert.Null(await Run());
        Assert.Single(_history.Read());
    }

    [Fact]
    public async Task Updates_are_counted_recorded_and_notified() // R1.3, R2.1
    {
        Installed(Pkg.Installed("A", "Node.js", update: true), Pkg.Installed("B", "Git", update: true), Pkg.Installed("C"));

        var record = await Run();

        Assert.Equal(CheckResult.Updates, record!.Result);
        Assert.Equal(2, record.UpdateCount);
        Assert.Equal(["Git", "Node.js"], record.Names!);
        Assert.Equal(("2 updates available", "Git, Node.js"), Assert.Single(_notifier.Shown));
    }

    [Fact]
    public async Task Packages_WinGet_does_not_manage_do_not_count() // R1.3
    {
        Installed(Pkg.Installed("A", update: true, source: ""));

        Assert.Equal(CheckResult.None, (await Run())!.Result);
    }

    [Fact]
    public async Task No_updates_records_but_does_not_notify() // R2.4
    {
        Installed(Pkg.Installed("A"));

        Assert.Equal(CheckResult.None, (await Run())!.Result);
        Assert.Empty(_notifier.Shown);
        Assert.Single(_history.Read());
    }

    [Fact]
    public async Task Excluded_packages_are_left_out_and_counted_as_ignored() // R5.2, R5.8
    {
        _settings.Current = _settings.Current with { ExcludedPackageIds = ["git.git"] };
        Installed(Pkg.Installed("Git.Git", "Git", update: true), Pkg.Installed("B", "Python", update: true));

        var record = await Run();

        Assert.Equal((1, 1), (record!.UpdateCount, record.ExcludedCount));
        Assert.Equal(["Python"], record.Names!);
        Assert.Equal(("1 update available, 1 ignored", "Python"), Assert.Single(_notifier.Shown));
    }

    [Fact]
    public async Task Only_excluded_packages_means_no_notification() // R5.2
    {
        _settings.Current = _settings.Current with { ExcludedPackageIds = ["A"] };
        Installed(Pkg.Installed("A", update: true));

        var record = await Run();

        Assert.Equal((CheckResult.None, 1), (record!.Result, record.ExcludedCount));
        Assert.Empty(_notifier.Shown);
    }

    [Fact]
    public async Task Skipped_while_operations_run_with_no_retry() // R1.5
    {
        _activity.OperationsRunning = true;

        Assert.Equal(CheckResult.SkippedBusy, (await Run())!.Result);
        Assert.Equal(0, _packages.InstalledCalls);

        _activity.OperationsRunning = false;
        Assert.Null(await Run());
    }

    [Fact]
    public async Task Skipped_on_a_metered_connection() // R1.10
    {
        _network.IsMetered = true;

        Assert.Equal(CheckResult.SkippedMetered, (await Run())!.Result);
        Assert.Equal(0, _packages.InstalledCalls);
    }

    [Fact]
    public async Task No_notification_while_the_window_is_in_front() // R2.5
    {
        _activity.WindowInForeground = true;
        Installed(Pkg.Installed("A", update: true));

        Assert.Equal(CheckResult.Updates, (await Run())!.Result);
        Assert.Empty(_notifier.Shown);
    }

    [Fact]
    public async Task Refreshes_sources_that_are_not_explicit_first() // R1.9
    {
        _packages.Sources.AddRange([Source("winget"), Source("msstore"), Source("winget-font", isExplicit: true)]);

        await Run();

        Assert.Equal(["winget", "msstore"], _packages.Refreshed);
    }

    [Fact]
    public async Task A_failed_refresh_goes_on_with_what_WinGet_has() // R1.9
    {
        _packages.Sources.Add(Source("winget"));
        _packages.RefreshErrors["winget"] = "Couldn't reach the source.";
        Installed(Pkg.Installed("A", update: true));

        Assert.Equal(CheckResult.Updates, (await Run())!.Result);
    }

    [Fact]
    public async Task A_failure_records_a_reason_and_shows_nothing() // R1.6
    {
        _packages.Installed = _ => throw new WinGetException("Couldn't reach the package sources.", null);

        var record = await Run();

        Assert.Equal((CheckResult.Failed, "Couldn't reach the package sources."), (record!.Result, record.Reason));
        Assert.Empty(_notifier.Shown);
    }

    [Fact]
    public async Task Any_exception_is_a_failed_check_not_a_crash() // R1.6
    {
        _packages.Installed = _ => throw new InvalidOperationException("Boom");

        Assert.Equal(CheckResult.Failed, (await Run())!.Result);
    }

    [Fact]
    public async Task Cancelled_by_Windows_says_so()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        _packages.Installed = t => Task.FromCanceled<IReadOnlyList<PackageInfo>>(t);

        var record = await _runner.RunAsync(CheckTrigger.Schedule, cancelled.Token);

        Assert.Equal("Windows stopped the check.", record!.Reason);
    }

    [Fact]
    public async Task Settings_that_cannot_be_read_fail_the_check() // design, error handling
    {
        _settings.WasReset = true;
        Installed(Pkg.Installed("A", update: true));

        Assert.Equal(CheckResult.Failed, (await Run())!.Result);
        Assert.Empty(_notifier.Shown);
    }

    [Fact]
    public async Task A_missed_check_is_recorded_as_one() // R3.11
    {
        var record = await _runner.RunAsync(CheckTrigger.MissedCheck);

        Assert.Equal(CheckTrigger.MissedCheck, record!.Trigger);
    }

    [Fact]
    public async Task The_next_check_follows_the_last_one() // R3.10
    {
        await Run();

        Assert.Equal(Now.AddHours(6), _runner.NextDue());
    }

    [Theory]
    [InlineData(1, 0, "1 update available")]
    [InlineData(3, 0, "3 updates available")]
    [InlineData(3, 1, "3 updates available, 1 ignored")]
    public void Title_counts_updates_and_ignored(int updates, int excluded, string expected) // R2.1, R5.2
    {
        Assert.Equal(expected, BackgroundCheckRunner.NotificationTitle(updates, excluded));
    }

    [Fact]
    public void Body_shows_three_names_then_the_rest_as_a_number() // R2.1
    {
        Assert.Equal("A, B, C", BackgroundCheckRunner.NotificationBody(["A", "B", "C"]));
        Assert.Equal("A, B, C and 2 more", BackgroundCheckRunner.NotificationBody(["A", "B", "C", "D", "E"]));
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FakeSettings : ISettings
    {
        public AppSettings Current { get; set; } = new();

        public bool WasReset { get; set; }

        public event EventHandler? Changed { add { } remove { } }

        public void Update(Func<AppSettings, AppSettings> change) => Current = change(Current);
    }

    private sealed class FakeNotifier : INotifier
    {
        public List<(string Title, string Body)> Shown { get; } = [];

        public void ShowUpdates(string title, string body) => Shown.Add((title, body));
    }

    private sealed class FakeActivity : IAppActivity
    {
        public bool OperationsRunning { get; set; }

        public bool WindowInForeground { get; set; }
    }

    private sealed class FakeNetwork : INetworkState
    {
        public bool IsMetered { get; set; }
    }
}
