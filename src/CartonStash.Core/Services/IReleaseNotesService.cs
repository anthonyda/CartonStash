using CartonStash.Core.Models;

namespace CartonStash.Core.Services;

/// <summary>Release notes from GitHub, fetched only when the user asks (spec 012 R2.2).</summary>
public interface IReleaseNotesService
{
    /// <summary>
    /// Asks GitHub for one release (R2.3), or returns it from the session cache (R2.9). Failures come back as a result,
    /// never an exception (R2.11); only the caller's own cancellation throws (R2.10).
    /// </summary>
    Task<GitHubNotesResult> GetGitHubNotesAsync(GitHubRelease release, CancellationToken cancellationToken);

    /// <summary>Notes already loaded this session, without a request (R2.9).</summary>
    GitHubNotes? TryGetCached(GitHubRelease release);
}
