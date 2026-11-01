namespace CartonStash.Core.Models;

/// <summary>What started a background check (010 R3.11).</summary>
public enum CheckTrigger
{
    Schedule,
    MissedCheck,
}

public enum CheckResult
{
    Updates,
    None,
    SkippedBusy,
    SkippedMetered,
    Failed,
}

/// <summary>One line of Check history (010 R3.11). <see cref="Reason"/> is set on a failure.</summary>
public sealed record CheckRecord(
    DateTimeOffset Time,
    CheckTrigger Trigger,
    CheckResult Result,
    int UpdateCount = 0,
    int ExcludedCount = 0,
    IReadOnlyList<string>? Names = null,
    string? Reason = null);
