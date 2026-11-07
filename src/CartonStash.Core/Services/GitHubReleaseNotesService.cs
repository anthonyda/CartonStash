using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using CartonStash.Core.Models;

namespace CartonStash.Core.Services;

/// <summary>
/// Fetches one release from GitHub's public API (spec 012 R2.3): no sign-in, no token, no cookies, https to
/// api.github.com only (R2.12). Successful answers are kept for the session (R2.9).
/// </summary>
public sealed class GitHubReleaseNotesService : IReleaseNotesService
{
    public const string ApiHost = "api.github.com";

    /// <summary>R2.12: answers larger than this are refused.</summary>
    public const int MaxResponseBytes = 1024 * 1024;

    /// <summary>R2.11: the whole request, redirects included.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    /// <summary>GitHub answers a renamed repository with a redirect; more than this is refused.</summary>
    public const int MaxRedirects = 2;

    private readonly HttpClient _client;
    private readonly string _userAgent;
    private readonly TimeSpan _timeout;
    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<GitHubRelease, GitHubNotes> _cache = new();

    /// <summary>For the app: a handler that follows no redirects and keeps no cookies.</summary>
    public GitHubReleaseNotesService(string userAgent)
        : this(CreateHandler(), userAgent)
    {
    }

    /// <param name="userAgent">GitHub refuses requests without one, e.g. "CartonStash/1.2.3". No contact details.</param>
    public GitHubReleaseNotesService(HttpMessageHandler handler, string userAgent, TimeSpan? timeout = null, TimeProvider? time = null)
    {
        _client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        // A version the header can't hold (spaces, odd characters) mustn't make every request fail.
        _userAgent = ProductInfoHeaderValue.TryParse(userAgent, out _) ? userAgent : AppInfo.Name;
        _timeout = timeout ?? DefaultTimeout;
        _time = time ?? TimeProvider.System;
    }

    public static SocketsHttpHandler CreateHandler() => new()
    {
        AllowAutoRedirect = false, // followed by hand, only to api.github.com (R2.12)
        UseCookies = false,
        AutomaticDecompression = DecompressionMethods.All,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    };

    public GitHubNotes? TryGetCached(GitHubRelease release) => _cache.TryGetValue(release, out var notes) ? notes : null;

    public async Task<GitHubNotesResult> GetGitHubNotesAsync(GitHubRelease release, CancellationToken cancellationToken)
    {
        if (TryGetCached(release) is { } cached)
        {
            return GitHubNotesResult.Loaded(cached);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);
        try
        {
            var result = await RequestAsync(release, timeout.Token).ConfigureAwait(false);
            if (result.Notes is { } notes)
            {
                _cache[release] = notes;
            }
            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new GitHubNotesResult(GitHubNotesOutcome.Unreachable); // our timeout
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException && !cancellationToken.IsCancellationRequested)
        {
            return new GitHubNotesResult(GitHubNotesOutcome.Unreachable);
        }
        catch (JsonException)
        {
            return new GitHubNotesResult(GitHubNotesOutcome.Failed);
        }
    }

    private async Task<GitHubNotesResult> RequestAsync(GitHubRelease release, CancellationToken cancellationToken)
    {
        var uri = release.ApiUri;
        for (var redirects = 0; ; redirects++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
            request.Headers.UserAgent.ParseAdd(_userAgent);

            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            switch ((int)response.StatusCode)
            {
                case 200:
                    return await ReadAsync(release, response, cancellationToken).ConfigureAwait(false);

                case 301 or 302 or 303 or 307 or 308:
                    if (redirects >= MaxRedirects || Redirect(uri, response.Headers.Location) is not { } next)
                    {
                        return new GitHubNotesResult(GitHubNotesOutcome.Failed);
                    }
                    uri = next;
                    continue;

                case 404:
                    return new GitHubNotesResult(GitHubNotesOutcome.NotFound);

                case 403 or 429 when RetryAt(response) is { } retryAt:
                    return new GitHubNotesResult(GitHubNotesOutcome.RateLimited, RetryAt: retryAt);

                default:
                    return new GitHubNotesResult(GitHubNotesOutcome.Failed);
            }
        }
    }

    /// <summary>R2.12: https on api.github.com only.</summary>
    private static Uri? Redirect(Uri from, Uri? location)
    {
        if (location is null)
        {
            return null;
        }
        var next = location.IsAbsoluteUri ? location : new Uri(from, location);
        return next.Scheme == Uri.UriSchemeHttps
            && string.Equals(next.Host, ApiHost, StringComparison.OrdinalIgnoreCase)
            && next.IsDefaultPort
            && next.UserInfo.Length == 0
            ? next
            : null;
    }

    /// <summary>GitHub's primary limit (x-ratelimit-remaining: 0 and x-ratelimit-reset) or its secondary one (retry-after).</summary>
    private DateTimeOffset? RetryAt(HttpResponseMessage response)
    {
        if (Header(response, "x-ratelimit-remaining") == "0"
            && long.TryParse(Header(response, "x-ratelimit-reset"), NumberStyles.None, CultureInfo.InvariantCulture, out var reset))
        {
            return DateTimeOffset.FromUnixTimeSeconds(reset);
        }
        return response.Headers.RetryAfter switch
        {
            { Delta: { } delta } => _time.GetUtcNow() + delta,
            { Date: { } date } => date,
            _ => null,
        };
    }

    private static string? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault()?.Trim() : null;

    private static async Task<GitHubNotesResult> ReadAsync(GitHubRelease release, HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength > MaxResponseBytes)
        {
            return new GitHubNotesResult(GitHubNotesOutcome.Failed);
        }
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            using var buffer = new MemoryStream();
            var chunk = new byte[16 * 1024];
            int read;
            while ((read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (buffer.Length + read > MaxResponseBytes)
                {
                    return new GitHubNotesResult(GitHubNotesOutcome.Failed);
                }
                buffer.Write(chunk, 0, read);
            }

            var json = JsonSerializer.Deserialize(buffer.GetBuffer().AsSpan(0, (int)buffer.Length), GitHubJsonContext.Default.GitHubReleaseJson);
            if (json is null)
            {
                return new GitHubNotesResult(GitHubNotesOutcome.Failed);
            }
            var title = FirstText(json.Name, json.TagName, release.Tag);
            // The page must be the release's own page on github.com; anything else falls back to the one built from the URL.
            var page = GitHubRelease.TryParse(json.HtmlUrl) is not null ? new Uri(json.HtmlUrl!) : PageOf(release);
            return GitHubNotesResult.Loaded(new GitHubNotes(title, json.PublishedAt, page, string.IsNullOrWhiteSpace(json.Body) ? null : json.Body));
        }
    }

    private static Uri PageOf(GitHubRelease release) =>
        new($"https://github.com/{release.Owner}/{release.Repo}/releases/tag/{Uri.EscapeDataString(release.Tag)}");

    private static string FirstText(params string?[] values) =>
        values.Select(v => v?.Trim()).FirstOrDefault(v => !string.IsNullOrEmpty(v)) ?? "";
}

internal sealed record GitHubReleaseJson(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("tag_name")] string? TagName,
    [property: JsonPropertyName("published_at")] DateTimeOffset? PublishedAt,
    [property: JsonPropertyName("body")] string? Body,
    [property: JsonPropertyName("html_url")] string? HtmlUrl);

[JsonSerializable(typeof(GitHubReleaseJson))]
internal sealed partial class GitHubJsonContext : JsonSerializerContext;
