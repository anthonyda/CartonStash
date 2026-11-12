using CartonStash.Core.Models;
using CartonStash.Core.Services;

namespace CartonStash.Core.Tests;

/// <summary>Spec 010. Paris: clocks go forward on 2026-03-29 at 02:00 and back on 2026-10-25 at 03:00.</summary>
public sealed class CheckScheduleTests
{
    private static readonly TimeZoneInfo Paris = TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time");
    private static readonly TimeSpan Winter = TimeSpan.FromHours(1);
    private static readonly TimeSpan Summer = TimeSpan.FromHours(2);

    private static readonly AppSettings Daily = new() { CheckMode = CheckMode.DailyTime, CheckTime = new TimeOnly(22, 0) };
    private static readonly AppSettings Every6Hours = new() { CheckMode = CheckMode.Interval, CheckIntervalHours = 6 };

    private static DateTimeOffset At(int month, int day, int hour, int minute = 0) => new(2026, month, day, hour, minute, 0, Winter);

    [Fact]
    public void Off_is_never_due()
    {
        var off = new AppSettings();

        Assert.Null(CheckSchedule.NextDue(off, At(1, 1, 0), Paris));
        Assert.False(CheckSchedule.IsDue(off, At(1, 1, 0), At(6, 1, 0), Paris));
    }

    [Fact]
    public void Interval_is_due_that_long_after_the_last_check() // R3.1
    {
        Assert.Equal(At(1, 1, 16), CheckSchedule.NextDue(Every6Hours, At(1, 1, 10), Paris));
        Assert.False(CheckSchedule.IsDue(Every6Hours, At(1, 1, 10), At(1, 1, 15, 44), Paris));
        Assert.True(CheckSchedule.IsDue(Every6Hours, At(1, 1, 10), At(1, 1, 15, 45), Paris));
    }

    [Fact]
    public void Daily_is_due_at_the_next_chosen_time() // R3.1
    {
        Assert.Equal(At(1, 1, 22), CheckSchedule.NextDue(Daily, At(1, 1, 10), Paris));
        Assert.Equal(At(1, 2, 22), CheckSchedule.NextDue(Daily, At(1, 1, 22), Paris));
    }

    [Fact]
    public void Daily_timer_firing_early_is_not_due_until_15_minutes_before() // T1: a 20-minute timer fired after 7
    {
        Assert.False(CheckSchedule.IsDue(Daily, At(1, 1, 8), At(1, 1, 21, 44), Paris));
        Assert.True(CheckSchedule.IsDue(Daily, At(1, 1, 8), At(1, 1, 21, 45), Paris));
    }

    [Fact]
    public void A_check_run_a_little_early_still_counts_for_that_day() // R3.8
    {
        Assert.Equal(At(1, 2, 22), CheckSchedule.NextDue(Daily, At(1, 1, 21, 50), Paris));
    }

    [Fact]
    public void A_schedule_set_under_15_minutes_before_the_time_waits_for_tomorrow()
    {
        Assert.Equal(At(1, 2, 22), CheckSchedule.NextDue(Daily, At(1, 1, 21, 50), Paris));
        Assert.Equal(At(1, 1, 22), CheckSchedule.NextDue(Daily, At(1, 1, 21, 40), Paris));
    }

    [Fact]
    public void Several_missed_checks_count_as_one_and_the_next_keeps_the_chosen_time() // R3.10
    {
        var away = At(1, 5, 9);

        Assert.True(CheckSchedule.IsDue(Daily, At(1, 1, 22), away, Paris));
        Assert.False(CheckSchedule.IsDue(Daily, away, away.AddMinutes(1), Paris));
        Assert.Equal(At(1, 5, 22), CheckSchedule.NextDue(Daily, away, Paris));
    }

    [Fact]
    public void Nothing_missed_means_not_due_at_sign_in() // R3.10
    {
        Assert.False(CheckSchedule.IsDue(Daily, At(1, 1, 22, 5), At(1, 2, 9), Paris));
    }

    [Fact]
    public void Daily_time_keeps_the_local_time_across_daylight_saving() // R3.9
    {
        var next = CheckSchedule.NextDue(Daily, new DateTimeOffset(2026, 3, 28, 22, 5, 0, Winter), Paris);

        Assert.Equal(new DateTimeOffset(2026, 3, 29, 22, 0, 0, Summer), next);
    }

    [Fact]
    public void A_time_skipped_by_daylight_saving_moves_past_the_gap() // R3.9
    {
        var at0230 = new AppSettings { CheckMode = CheckMode.DailyTime, CheckTime = new TimeOnly(2, 30) };

        var next = CheckSchedule.NextDue(at0230, new DateTimeOffset(2026, 3, 28, 12, 0, 0, Winter), Paris);

        Assert.Equal(new DateTimeOffset(2026, 3, 29, 3, 0, 0, Summer), next);
    }

    [Fact]
    public void A_time_repeated_by_daylight_saving_runs_the_first_time() // R3.9
    {
        var at0230 = new AppSettings { CheckMode = CheckMode.DailyTime, CheckTime = new TimeOnly(2, 30) };

        var next = CheckSchedule.NextDue(at0230, new DateTimeOffset(2026, 10, 24, 12, 0, 0, Summer), Paris);

        Assert.Equal(new DateTimeOffset(2026, 10, 25, 2, 30, 0, Summer), next);
    }

    [Theory]
    [InlineData(600, 600)]
    [InlineData(599.5, 600)]
    [InlineData(20, 20)]
    [InlineData(14, 15)]
    [InlineData(-5, 15)]
    public void Timer_is_in_whole_minutes_and_never_below_the_minimum(double minutes, uint expected)
    {
        var now = At(1, 1, 0);

        Assert.Equal(expected, CheckSchedule.TimerMinutes(now.AddMinutes(minutes), now));
    }

    [Fact] // BR-27: every 24 hours, timer at 9:00; yesterday's check ran 9:20 to 9:25 after a late wake
    public void The_interval_timer_counts_a_quarter_early_R1_3()
    {
        var daily = new AppSettings { CheckMode = CheckMode.Interval, CheckIntervalHours = 24 };
        var lastStarted = At(1, 1, 9, 20);

        Assert.True(CheckSchedule.IsDue(daily, lastStarted, At(1, 2, 9), Paris, fromTimer: true));   // the next tick, 23 h 40 min later
        Assert.False(CheckSchedule.IsDue(daily, lastStarted, At(1, 2, 9), Paris));                   // an unlock keeps 15 minutes
        Assert.False(CheckSchedule.IsDue(daily, lastStarted, At(1, 2, 3), Paris, fromTimer: true));  // not 6 hours early
        Assert.True(CheckSchedule.IsDue(Every6Hours, At(1, 1, 10), At(1, 1, 15, 45), Paris, fromTimer: true)); // never less than 15 minutes
    }
}
