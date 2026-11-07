using System.Net;
using System.Text;
using CartonStash.Core.Models;
using CartonStash.Core.Services;

namespace CartonStash.Core.Tests;

/// <summary>Spec 012 R2.3, R2.9, R2.11, R2.12, against a fake handler: no network.</summary>
public class GitHubReleaseNotesServiceTests
{
    private static readonly GitHubRelease Bat = new("sharkdp", "bat", "v0.26.1");

    private const string BatJson = """
        {"name":"v0.26.1","tag_name":"v0.26.1","published_at":"2026-08-01T10:00:00Z",
         "html_url":"https://github.com/sharkdp/bat/releases/tag/v0.26.1","body":"## Features\n- Paging"}
        """;

    private sealed class FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> answer) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return answer(request, cancellationToken);
        }
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static (GitHubReleaseNotesService Service, FakeHandler Handler) Create(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> answer, TimeSpan? timeout = null)
    {
        var handler = new FakeHandler(answer);
        return (new GitHubReleaseNotesService(handler, "CartonStash/1.2.3", timeout), handler);
    }

    private static (GitHubReleaseNotesService Service, FakeHandler Handler) Create(Func<HttpResponseMessage> answer) =>
        Create((_, _) => Task.FromResult(answer()));

    [Fact]
    public async Task Asks_for_one_release_with_no_credentials() // R2.3
    {
        var (service, handler) = Create(() => Json(BatJson));
        await service.GetGitHubNotesAsync(new GitHubRelease("owner", "repo", "app/v1.0"), default);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://api.github.com/repos/owner/repo/releases/tags/app%2Fv1.0", request.RequestUri!.AbsoluteUri);
        Assert.Equal("application/vnd.github+json", request.Headers.Accept.Single().MediaType);
        Assert.Equal("2022-11-28", request.Headers.GetValues("X-GitHub-Api-Version").Single());
        Assert.Equal("CartonStash/1.2.3", request.Headers.UserAgent.ToString());
        Assert.Null(request.Headers.Authorization);
        Assert.False(request.Headers.Contains("Cookie"));
    }

    [Fact]
    public async Task A_user_agent_the_header_cant_hold_falls_back_to_the_name() // R2.3
    {
        var handler = new FakeHandler((_, _) => Task.FromResult(Json(BatJson)));
        var service = new GitHubReleaseNotesService(handler, "CartonStash/1.0 (odd version)");
        await service.GetGitHubNotesAsync(Bat, default);
        Assert.Equal("CartonStash", handler.Requests[0].Headers.UserAgent.ToString());
    }

    [Fact]
    public void The_apps_user_agent_is_name_and_version() // R2.3
    {
        Assert.Matches(@"^CartonStash/\d+\.\d+\.\d+", CartonStash.Core.AppInfo.UserAgent);
    }

    [Fact]
    public async Task Loaded_release() // R2.4
    {
        var (service, _) = Create(() => Json(BatJson));
        var result = await service.GetGitHubNotesAsync(Bat, default);

        Assert.Equal(GitHubNotesOutcome.Loaded, result.Outcome);
        Assert.Equal(new GitHubNotes("v0.26.1", new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero),
            new Uri("https://github.com/sharkdp/bat/releases/tag/v0.26.1"), "## Features\n- Paging"), result.Notes);
    }

    [Fact]
    public async Task A_release_with_no_notes_or_name()
    {
        var (service, _) = Create(() => Json("""{"name":"","tag_name":"v2","body":null,"html_url":"https://evil.example/x"}"""));
        var notes = (await service.GetGitHubNotesAsync(Bat, default)).Notes!;

        Assert.Null(notes.Body);
        Assert.Equal("v2", notes.Title);
        Assert.Null(notes.PublishedAt);
        // Not a github.com release page: the page built from the URL is used for relative links instead.
        Assert.Equal("https://github.com/sharkdp/bat/releases/tag/v0.26.1", notes.Page.AbsoluteUri);
    }

    [Fact]
    public async Task Not_found() // R2.11
    {
        var (service, _) = Create(() => Json("""{"message":"Not Found"}""", HttpStatusCode.NotFound));
        Assert.Equal(GitHubNotesOutcome.NotFound, (await service.GetGitHubNotesAsync(Bat, default)).Outcome);
    }

    [Fact]
    public async Task Rate_limited_with_GitHubs_reset_time() // R2.11
    {
        var (service, _) = Create(() =>
        {
            var response = Json("""{"message":"API rate limit exceeded"}""", HttpStatusCode.Forbidden);
            response.Headers.Add("x-ratelimit-remaining", "0");
            response.Headers.Add("x-ratelimit-reset", "1794065520");
            return response;
        });
        var result = await service.GetGitHubNotesAsync(Bat, default);

        Assert.Equal(GitHubNotesOutcome.RateLimited, result.Outcome);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1794065520), result.RetryAt);
    }

    [Fact]
    public async Task Rate_limited_with_retry_after() // R2.11
    {
        var before = DateTimeOffset.UtcNow;
        var (service, _) = Create(() =>
        {
            var response = Json("{}", HttpStatusCode.TooManyRequests);
            response.Headers.Add("retry-after", "120");
            return response;
        });
        var result = await service.GetGitHubNotesAsync(Bat, default);

        Assert.Equal(GitHubNotesOutcome.RateLimited, result.Outcome);
        Assert.InRange(result.RetryAt!.Value, before.AddSeconds(119), DateTimeOffset.UtcNow.AddSeconds(121));
    }

    [Fact]
    public async Task A_403_that_isnt_the_limit_is_a_failure() // R2.11
    {
        var (service, _) = Create(() => Json("{}", HttpStatusCode.Forbidden));
        Assert.Equal(GitHubNotesOutcome.Failed, (await service.GetGitHubNotesAsync(Bat, default)).Outcome);
    }

    [Theory] // R2.11
    [InlineData(HttpStatusCode.InternalServerError, "{}")]
    [InlineData(HttpStatusCode.OK, "not json")]
    [InlineData(HttpStatusCode.OK, "null")]
    public async Task Server_errors_and_bad_answers_are_failures(HttpStatusCode status, string body)
    {
        var (service, _) = Create(() => Json(body, status));
        Assert.Equal(GitHubNotesOutcome.Failed, (await service.GetGitHubNotesAsync(Bat, default)).Outcome);
    }

    [Fact]
    public async Task No_connection_is_unreachable() // R2.11
    {
        var (service, _) = Create((_, _) => throw new HttpRequestException("No such host is known."));
        Assert.Equal(GitHubNotesOutcome.Unreachable, (await service.GetGitHubNotesAsync(Bat, default)).Outcome);
    }

    [Fact]
    public async Task No_answer_in_time_is_unreachable() // R2.11
    {
        var (service, _) = Create(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return Json(BatJson);
        }, timeout: TimeSpan.FromMilliseconds(50));
        Assert.Equal(GitHubNotesOutcome.Unreachable, (await service.GetGitHubNotesAsync(Bat, default)).Outcome);
    }

    [Fact]
    public async Task The_callers_cancellation_throws() // R2.10
    {
        using var cancel = new CancellationTokenSource();
        var (service, _) = Create(async (_, ct) =>
        {
            await cancel.CancelAsync();
            await Task.Delay(Timeout.Infinite, ct);
            return Json(BatJson);
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetGitHubNotesAsync(Bat, cancel.Token));
    }

    private static HttpResponseMessage Moved(string location)
    {
        var response = new HttpResponseMessage(HttpStatusCode.MovedPermanently);
        response.Headers.Location = new Uri(location, UriKind.RelativeOrAbsolute);
        return response;
    }

    [Fact]
    public async Task Follows_a_redirect_to_the_api() // R2.12: a renamed repository
    {
        var (service, handler) = Create((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath.StartsWith("/repos/", StringComparison.Ordinal)
            ? Moved("https://api.github.com/repositories/123/releases/tags/v0.26.1")
            : Json(BatJson)));
        var result = await service.GetGitHubNotesAsync(Bat, default);

        Assert.Equal(GitHubNotesOutcome.Loaded, result.Outcome);
        Assert.Equal("https://api.github.com/repositories/123/releases/tags/v0.26.1", handler.Requests[1].RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task Follows_a_relative_redirect_on_the_api() // R2.12
    {
        var (service, handler) = Create((request, _) => Task.FromResult(IsOriginal(request) ? Moved("/repositories/123/releases/tags/v0.26.1") : Json(BatJson)));
        Assert.Equal(GitHubNotesOutcome.Loaded, (await service.GetGitHubNotesAsync(Bat, default)).Outcome);
        Assert.Equal("https://api.github.com/repositories/123/releases/tags/v0.26.1", handler.Requests[1].RequestUri!.AbsoluteUri);

        static bool IsOriginal(HttpRequestMessage request) => request.RequestUri!.AbsolutePath.StartsWith("/repos/", StringComparison.Ordinal);
    }

    [Theory] // R2.12
    [InlineData("https://evil.example/repos/x")]
    [InlineData("http://api.github.com/repos/x")]
    [InlineData("https://api.github.com:8443/repos/x")]
    [InlineData("https://github.com/sharkdp/bat")]
    public async Task Refuses_a_redirect_elsewhere(string location)
    {
        var (service, handler) = Create(() => Moved(location));
        Assert.Equal(GitHubNotesOutcome.Failed, (await service.GetGitHubNotesAsync(Bat, default)).Outcome);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Refuses_a_third_redirect() // R2.12
    {
        var (service, handler) = Create(() => Moved("https://api.github.com/repositories/1"));
        Assert.Equal(GitHubNotesOutcome.Failed, (await service.GetGitHubNotesAsync(Bat, default)).Outcome);
        Assert.Equal(1 + GitHubReleaseNotesService.MaxRedirects, handler.Requests.Count);
    }

    [Fact]
    public async Task Refuses_an_answer_said_to_be_too_large() // R2.12
    {
        var (service, _) = Create(() =>
        {
            var response = Json(BatJson);
            response.Content.Headers.ContentLength = GitHubReleaseNotesService.MaxResponseBytes + 1;
            return response;
        });
        Assert.Equal(GitHubNotesOutcome.Failed, (await service.GetGitHubNotesAsync(Bat, default)).Outcome);
    }

    [Fact]
    public async Task Stops_reading_an_answer_that_turns_out_too_large() // R2.12
    {
        var (service, _) = Create(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            // No Content-Length: the size is only known while reading.
            Content = new StreamContent(new EndlessStream()),
        });
        Assert.Equal(GitHubNotesOutcome.Failed, (await service.GetGitHubNotesAsync(Bat, default)).Outcome);
    }

    [Fact]
    public async Task An_answer_just_under_the_cap_is_read()
    {
        var padding = new string(' ', GitHubReleaseNotesService.MaxResponseBytes - BatJson.Length - 10);
        var (service, _) = Create(() => Json(BatJson + padding));
        Assert.Equal(GitHubNotesOutcome.Loaded, (await service.GetGitHubNotesAsync(Bat, default)).Outcome);
    }

    [Fact]
    public async Task Loaded_notes_are_kept_for_the_session() // R2.9
    {
        var (service, handler) = Create(() => Json(BatJson));
        Assert.Null(service.TryGetCached(Bat));

        var first = await service.GetGitHubNotesAsync(Bat, default);
        var second = await service.GetGitHubNotesAsync(Bat, default);

        Assert.Single(handler.Requests);
        Assert.Same(first.Notes, second.Notes);
        Assert.Same(first.Notes, service.TryGetCached(Bat));
    }

    [Fact]
    public async Task Failures_are_not_kept() // R2.9, R2.11: Retry really asks again
    {
        var calls = 0;
        var (service, handler) = Create(() => ++calls == 1 ? Json("{}", HttpStatusCode.BadGateway) : Json(BatJson));

        Assert.Equal(GitHubNotesOutcome.Failed, (await service.GetGitHubNotesAsync(Bat, default)).Outcome);
        Assert.Null(service.TryGetCached(Bat));
        Assert.Equal(GitHubNotesOutcome.Loaded, (await service.GetGitHubNotesAsync(Bat, default)).Outcome);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public void The_apps_handler_follows_no_redirects_and_keeps_no_cookies() // R2.3, R2.12
    {
        using var handler = GitHubReleaseNotesService.CreateHandler();
        Assert.False(handler.AllowAutoRedirect);
        Assert.False(handler.UseCookies);
    }

    private sealed class EndlessStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count)
        {
            Array.Fill(buffer, (byte)' ', offset, count);
            return count;
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

/// <summary>Spec 012 R2.3 against the real GitHub API: one anonymous request.</summary>
public class GitHubReleaseNotesServiceIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Fetches_an_old_bat_release()
    {
        var service = new GitHubReleaseNotesService("CartonStash-tests/1.0");
        var result = await service.GetGitHubNotesAsync(new GitHubRelease("sharkdp", "bat", "v0.26.1"), default);

        // Rate limits are shared per IP address: say so rather than fail for an unrelated reason.
        Assert.True(result.Outcome is GitHubNotesOutcome.Loaded or GitHubNotesOutcome.RateLimited, result.Outcome.ToString());
        if (result.Outcome == GitHubNotesOutcome.Loaded)
        {
            Assert.Equal("v0.26.1", result.Notes!.Title);
            Assert.Equal("https://github.com/sharkdp/bat/releases/tag/v0.26.1", result.Notes.Page.AbsoluteUri);
            Assert.False(string.IsNullOrWhiteSpace(result.Notes.Body));
        }
    }
}
