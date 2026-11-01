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

/// <summary>Per-user app settings (spec 006), saved as JSON.</summary>
public sealed record AppSettings
{
    public AppTheme Theme { get; init; } = AppTheme.System;

    public InstallScope InstallScope { get; init; } = InstallScope.Any;

    /// <summary>Missing in files saved before 006 R5, which then keep this default (R5.4).</summary>
    public StartPage StartPage { get; init; } = StartPage.Search;

    /// <summary>Keep the Activity pane open beside the page (009 R3.4).</summary>
    public bool ActivityPinned { get; init; }

    /// <summary>The user's answer per source ID. Missing in files saved before 006 R3.8, which then count as unanswered.</summary>
    public IReadOnlyDictionary<string, SourceAgreementAnswer> SourceAgreementAnswers { get; init; } = new Dictionary<string, SourceAgreementAnswer>();

    // By hand because a record compares a dictionary by reference, and SettingsService.Update skips equal settings.
    public bool Equals(AppSettings? other) =>
        other is not null
        && (Theme, InstallScope, StartPage, ActivityPinned) == (other.Theme, other.InstallScope, other.StartPage, other.ActivityPinned)
        && SourceAgreementAnswers.Count == other.SourceAgreementAnswers.Count
        && SourceAgreementAnswers.All(a => other.SourceAgreementAnswers.TryGetValue(a.Key, out var answer) && a.Value == answer);

    public override int GetHashCode() => HashCode.Combine(Theme, InstallScope, StartPage, ActivityPinned, SourceAgreementAnswers.Count);
}

/// <summary>What the user answered for a source's agreements, and which text they saw (006 R3.9).</summary>
public sealed record SourceAgreementAnswer(bool Accepted, string TextHash);

/// <summary>A configured WinGet source (006 R3.1).</summary>
public sealed record SourceInfo(
    string Name,
    string Type,
    string Argument,
    bool IsTrusted,
    bool IsExplicit,
    DateTimeOffset? LastUpdated,
    IReadOnlyList<Agreement> Agreements)
{
    public string TrustText => IsTrusted ? "Trusted" : "Not trusted";

    public string LastUpdatedText => LastUpdated is { } t ? $"updated {t.LocalDateTime:g}" : "never updated";

    /// <summary>"Microsoft.PreIndexed.Package · https://… · explicit"</summary>
    public string DetailText => string.Join(" · ", new[] { Type, Argument, IsExplicit ? "explicit (excluded from default searches)" : null }
        .Where(s => !string.IsNullOrEmpty(s)));

    public bool HasAgreements => Agreements.Count > 0;
}
