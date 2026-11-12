namespace CartonStash.Core.Models;

public enum AppTheme
{
    System,
    Light,
    Dark,
}

/// <summary>Default install scope (006 R2.1). <see cref="Any"/> lets WinGet decide.</summary>
public enum InstallScope
{
    Any,
    User,
    System,
}

/// <summary>The page the app opens on (006 R5.1). Values match the navigation order.</summary>
public enum StartPage
{
    Search,
    Installed,
    Updates,
}

/// <summary>When background update checks run (010 R3.1): one mode at a time.</summary>
public enum CheckMode
{
    Off,
    Interval,
    DailyTime,
}

/// <summary>Per-user app settings (spec 006), saved as JSON.</summary>
public sealed record AppSettings
{
    /// <summary>The choices for <see cref="CheckIntervalHours"/> (010 R3.1): 1, 6, 12 or 24 hours, or 7 days.</summary>
    public static readonly IReadOnlyList<int> CheckIntervals = [1, 6, 12, 24, 168];

    public AppTheme Theme { get; init; } = AppTheme.System;

    public InstallScope InstallScope { get; init; } = InstallScope.Any;

    /// <summary>Missing in files saved before 006 R5, which then keep this default (R5.4).</summary>
    public StartPage StartPage { get; init; } = StartPage.Search;

    /// <summary>Keep the Activity pane open beside the page (009 R3.4).</summary>
    public bool ActivityPinned { get; init; }

    /// <summary>The Activity pane's order (009 R4.8). Missing in older files, which then use Default.</summary>
    public ActivitySort ActivitySort { get; init; } = ActivitySort.Default;

    /// <summary>The main window's last size and position (006 R6.1). Null until the window first closes (R6.4).</summary>
    public WindowLayout? Window { get; init; }

    /// <summary>Whether the navigation pane was expanded (006 R6.1). Missing in older files, which keep it expanded.</summary>
    public bool NavPaneOpen { get; init; } = true;

    /// <summary>Missing in files saved before 010, which then keep checks off (R3.5).</summary>
    public CheckMode CheckMode { get; init; } = CheckMode.Off;

    public int CheckIntervalHours { get; init; } = 24;

    public TimeOnly CheckTime { get; init; } = new(22, 0);

    /// <summary>When the schedule was last changed: what counts as missed starts here, not from the last check (010 R3.10).</summary>
    public DateTimeOffset? CheckScheduleSetAt { get; init; }

    /// <summary>Packages left out of update checks, for every version (010 R5.3). Changes nothing in WinGet (R5.6).</summary>
    public IReadOnlyList<string> ExcludedPackageIds { get; init; } = [];

    public bool IsExcluded(string packageId) => ExcludedPackageIds.Contains(packageId, StringComparer.OrdinalIgnoreCase);

    public AppSettings WithExcluded(string packageId) =>
        IsExcluded(packageId) ? this : this with { ExcludedPackageIds = [.. ExcludedPackageIds, packageId] };

    public AppSettings WithoutExcluded(string packageId) => this with
    {
        ExcludedPackageIds = ExcludedPackageIds.Where(id => !string.Equals(id, packageId, StringComparison.OrdinalIgnoreCase)).ToList(),
    };

    /// <summary>
    /// 006 R3.15, R3.17: the IDs of explicit sources whose **Use in searches** box is checked. Missing in older files,
    /// which then leave every explicit source out (R3.14).
    /// </summary>
    public IReadOnlyList<string> SearchedExplicitSources { get; init; } = [];

    public bool IsSearched(string sourceId) => SearchedExplicitSources.Contains(sourceId, StringComparer.OrdinalIgnoreCase);

    public AppSettings WithSearched(string sourceId, bool searched) => this with
    {
        SearchedExplicitSources = searched
            ? IsSearched(sourceId) ? SearchedExplicitSources : [.. SearchedExplicitSources, sourceId]
            : SearchedExplicitSources.Where(id => !string.Equals(id, sourceId, StringComparison.OrdinalIgnoreCase)).ToList(),
    };

    /// <summary>The user's answer per source ID. Missing in files saved before 006 R3.8, which then count as unanswered.</summary>
    public IReadOnlyDictionary<string, SourceAgreementAnswer> SourceAgreementAnswers { get; init; } = new Dictionary<string, SourceAgreementAnswer>();

    // By hand because a record compares lists and dictionaries by reference, and SettingsService.Update skips equal settings.
    public bool Equals(AppSettings? other) =>
        other is not null
        && (Theme, InstallScope, StartPage, ActivityPinned) == (other.Theme, other.InstallScope, other.StartPage, other.ActivityPinned)
        && (CheckMode, CheckIntervalHours, CheckTime, CheckScheduleSetAt) == (other.CheckMode, other.CheckIntervalHours, other.CheckTime, other.CheckScheduleSetAt)
        && (Window, NavPaneOpen, ActivitySort) == (other.Window, other.NavPaneOpen, other.ActivitySort)
        && ExcludedPackageIds.SequenceEqual(other.ExcludedPackageIds, StringComparer.OrdinalIgnoreCase)
        && SearchedExplicitSources.SequenceEqual(other.SearchedExplicitSources, StringComparer.OrdinalIgnoreCase)
        && SourceAgreementAnswers.Count == other.SourceAgreementAnswers.Count
        && SourceAgreementAnswers.All(a => other.SourceAgreementAnswers.TryGetValue(a.Key, out var answer) && a.Value == answer);

    public override int GetHashCode() =>
        HashCode.Combine(Theme, InstallScope, StartPage, ActivityPinned, CheckMode, CheckIntervalHours, CheckTime, SourceAgreementAnswers.Count);
}

/// <summary>The main window's normal (not maximized) bounds in physical pixels, and whether it was maximized (006 R6).</summary>
public sealed record WindowLayout(int X, int Y, int Width, int Height, bool Maximized);

/// <summary>What the user answered for a source's agreements, and which text they saw (006 R3.9).</summary>
public sealed record SourceAgreementAnswer(bool Accepted, string TextHash);

/// <summary>
/// Where a source stands with its agreements (006 R3.8). <see cref="None"/>: it has none. <see cref="Unknown"/>: they
/// couldn't be read, because the text comes from the source.
/// </summary>
public enum AgreementStatus
{
    None,
    Unanswered,
    Accepted,
    Declined,
    Unknown,
}

/// <summary>A configured WinGet source (006 R3.1).</summary>
public sealed record SourceInfo(
    string Id,
    string Name,
    string Type,
    string Argument,
    bool IsTrusted,
    bool IsExplicit,
    DateTimeOffset? LastUpdated,
    IReadOnlyList<Agreement> Agreements,
    AgreementStatus AgreementStatus = AgreementStatus.None)
{
    /// <summary>
    /// 006 R3.14, R3.15: Search uses the source. Always true for a source that isn't explicit; for an explicit one, its
    /// <b>Use in searches</b> box.
    /// </summary>
    public bool UsedInSearches { get; init; } = !IsExplicit;

    public string TrustText => IsTrusted ? "Trusted" : "Not trusted";

    public string LastUpdatedText => LastUpdated is { } t ? $"updated {t.LocalDateTime:g}" : "never updated";

    /// <summary>"Microsoft.PreIndexed.Package · https://… · explicit"</summary>
    public string DetailText => string.Join(" · ", new[] { Type, Argument, IsExplicit ? "explicit" : null }
        .Where(s => !string.IsNullOrEmpty(s)));

    public bool HasAgreements => Agreements.Count > 0 || AgreementStatus == AgreementStatus.Declined; // a declined source isn't read (R3.8)

    /// <summary>The source's button in Settings (006 R3.10, R3.11).</summary>
    public string AgreementButtonText => AgreementStatus == AgreementStatus.Accepted ? "Withdraw" : "Read and accept";
}
