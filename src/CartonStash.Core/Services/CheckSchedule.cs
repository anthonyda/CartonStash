using CartonStash.Core.Models;

namespace CartonStash.Core.Services;

/// <summary>
/// When background checks are due (010). Windows can fire a timer early or late, so every trigger asks
/// <see cref="IsDue"/> instead of trusting that it means a check is due.
/// </summary>
public static class CheckSchedule
{
    /// <summary>Windows' shortest timer. A check this close to its time counts as due, so an early timer never leaves a shorter one.</summary>
    public static readonly TimeSpan MinimumTimer = TimeSpan.FromMinutes(15);

    /// <summary>
    /// The next time a check is due, or null when checks are off.
    /// <paramref name="since"/> is the later of the last check and <see cref="AppSettings.CheckScheduleSetAt"/>.
    /// </summary>
    public static DateTimeOffset? NextDue(AppSettings settings, DateTimeOffset since, TimeZoneInfo zone) => settings.CheckMode switch
    {
        CheckMode.Interval => since + TimeSpan.FromHours(settings.CheckIntervalHours),
        CheckMode.DailyTime => FirstOccurrenceAfter(settings.CheckTime, since + MinimumTimer, zone),
        _ => null,
    };

    /// <summary>What counts as missed starts here: the later of the last check and the last schedule change (R3.10).</summary>
    public static DateTimeOffset Since(AppSettings settings, CheckRecord? lastCheck, DateTimeOffset now)
    {
        var setAt = settings.CheckScheduleSetAt ?? now;
        return lastCheck is { } last && last.Time > setAt ? last.Time : setAt;
    }

    /// <summary>True from <see cref="MinimumTimer"/> before the next due time, so several missed checks count as one (R3.10).</summary>
    public static bool IsDue(AppSettings settings, DateTimeOffset since, DateTimeOffset now, TimeZoneInfo zone) =>
        NextDue(settings, since, zone) is { } next && now >= next - MinimumTimer;

    /// <summary>The whole minutes for a one-time timer to <paramref name="next"/>, never below Windows' minimum.</summary>
    public static uint TimerMinutes(DateTimeOffset next, DateTimeOffset now)
    {
        var delay = next - now;
        return (uint)Math.Ceiling((delay < MinimumTimer ? MinimumTimer : delay).TotalMinutes);
    }

    private static DateTimeOffset FirstOccurrenceAfter(TimeOnly time, DateTimeOffset after, TimeZoneInfo zone)
    {
        var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(after, zone).DateTime);
        while (true)
        {
            var occurrence = Occurrence(day, time, zone);
            if (occurrence > after)
            {
                return occurrence;
            }
            day = day.AddDays(1);
        }
    }

    /// <summary>The time on that local day (R3.9). A time skipped by daylight saving moves past the gap; a repeated one uses the first.</summary>
    private static DateTimeOffset Occurrence(DateOnly day, TimeOnly time, TimeZoneInfo zone)
    {
        var local = day.ToDateTime(time);
        while (zone.IsInvalidTime(local))
        {
            local = local.AddMinutes(30);
        }
        var offset = zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local).Max() : zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset);
    }
}
