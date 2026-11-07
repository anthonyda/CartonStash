namespace CartonStash.Core.Models;

/// <summary>One release's notes as GitHub returned them (spec 012 R2.4).</summary>
/// <param name="Title">The release name, or its tag when it has none.</param>
/// <param name="Page">The release page on github.com: relative links in <paramref name="Body"/> resolve against it (R2.7).</param>
/// <param name="Body">GitHub Markdown, or null when the release has no notes.</param>
public sealed record GitHubNotes(string Title, DateTimeOffset? PublishedAt, Uri Page, string? Body);

public enum GitHubNotesOutcome
{
    Loaded,

    /// <summary>No connection, or no answer within the timeout.</summary>
    Unreachable,

    /// <summary>GitHub's limit for requests without sign-in was reached; <see cref="GitHubNotesResult.RetryAt"/> says until when.</summary>
    RateLimited,

    NotFound,

    /// <summary>Anything else: a server error, bad JSON, an answer too large, a redirect to another host.</summary>
    Failed,
}

/// <summary>What asking GitHub for one release gave (R2.11).</summary>
public sealed record GitHubNotesResult(GitHubNotesOutcome Outcome, GitHubNotes? Notes = null, DateTimeOffset? RetryAt = null)
{
    public static GitHubNotesResult Loaded(GitHubNotes notes) => new(GitHubNotesOutcome.Loaded, notes);
}
