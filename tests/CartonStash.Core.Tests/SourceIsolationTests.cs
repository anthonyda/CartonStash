using CartonStash.Core.Models;
using CartonStash.Core.Services;

namespace CartonStash.Core.Tests;

public class SourceIsolationTests
{
    private const int Rest = unchecked((int)0x8A15003B);
    private const int Network = unchecked((int)0x80072EFD);

    /// <summary>A composite that fails while any bad source is in use, as WinGet's does.</summary>
    private sealed class FakeSources(string[] all, Dictionary<string, int> bad)
    {
        public List<SourceFailure> LeftOut { get; } = [];
        public int Attempts { get; private set; }
        public int Probes { get; private set; }
        public bool MayBeOneSource { get; init; } = true;

        private IReadOnlyList<string> InUse() => all.Where(s => LeftOut.All(f => f.Source != s)).ToList();

        public Task<string> RunAsync() => SourceIsolation.RunAsync(
            () =>
            {
                Attempts++;
                var failing = InUse().FirstOrDefault(bad.ContainsKey);
                return failing is null
                    ? Task.FromResult(string.Join(",", InUse()))
                    : throw new WinGetException("composite failed", bad[failing]) { MayBeOneSource = MayBeOneSource };
            },
            InUse,
            () => LeftOut,
            source =>
            {
                Probes++;
                return Task.FromResult<int?>(bad.TryGetValue(source, out var code) ? code : null);
            },
            (source, code) => new SourceFailure(source, $"{source} failed.", code),
            LeftOut.AddRange);
    }

    [Fact] // R1.1
    public async Task Working_sources_answer_when_one_fails()
    {
        var sources = new FakeSources(["msstore", "winget"], new() { ["msstore"] = Rest });

        var result = await sources.RunAsync();

        Assert.Equal("winget", result);
        var failure = Assert.Single(sources.LeftOut);
        Assert.Equal(("msstore", Rest), (failure.Source, failure.HResult));
    }

    [Fact] // R1.4
    public async Task Healthy_sources_get_no_extra_calls()
    {
        var sources = new FakeSources(["msstore", "winget"], []);

        await sources.RunAsync();

        Assert.Equal(1, sources.Attempts);
        Assert.Equal(0, sources.Probes);
    }

    [Fact] // R2.5
    public async Task When_all_fail_the_error_gives_each_reason_and_leaves_nothing_out()
    {
        var sources = new FakeSources(["msstore", "winget"], new() { ["msstore"] = Rest, ["winget"] = Network });

        var error = await Assert.ThrowsAsync<WinGetException>(sources.RunAsync);

        Assert.Equal("msstore failed. winget failed.", error.Message);
        Assert.Empty(sources.LeftOut);
    }

    [Fact] // R2.5: the last source failing still mentions the one left out earlier
    public async Task Earlier_failures_stay_in_the_error()
    {
        var sources = new FakeSources(["msstore", "winget"], new() { ["winget"] = Network });
        sources.LeftOut.Add(new SourceFailure("msstore", "msstore failed.", Rest));

        var error = await Assert.ThrowsAsync<WinGetException>(sources.RunAsync);

        Assert.Equal("msstore failed. winget failed.", error.Message);
    }

    [Fact]
    public async Task A_single_source_is_named_without_probing()
    {
        var sources = new FakeSources(["winget"], new() { ["winget"] = Network });

        var error = await Assert.ThrowsAsync<WinGetException>(sources.RunAsync);

        Assert.Equal("winget failed.", error.Message);
        Assert.Equal(Network, error.HResult);
        Assert.Equal(0, sources.Probes);
    }

    [Fact]
    public async Task Other_failures_pass_through_untouched() // e.g. BlockedByPolicy
    {
        var sources = new FakeSources(["msstore", "winget"], new() { ["msstore"] = Rest }) { MayBeOneSource = false };

        var error = await Assert.ThrowsAsync<WinGetException>(sources.RunAsync);

        Assert.Equal("composite failed", error.Message);
        Assert.Equal(0, sources.Probes);
    }

    [Fact]
    public async Task A_brief_failure_tries_once_more_and_reports_what_that_returns()
    {
        var attempts = 0;

        var result = await SourceIsolation.RunAsync(
            () => ++attempts == 1
                ? throw new WinGetException("brief", Network) { MayBeOneSource = true }
                : Task.FromResult("ok"),
            () => ["msstore", "winget"],
            () => [],
            _ => Task.FromResult<int?>(null),
            (s, c) => new SourceFailure(s, s, c),
            _ => Assert.Fail("Nothing should be left out"));

        Assert.Equal("ok", result);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task A_cancelled_probe_never_counts_as_a_failed_source()
    {
        var leftOut = new List<SourceFailure>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SourceIsolation.RunAsync<string>(
            () => throw new WinGetException("failed", Network) { MayBeOneSource = true },
            () => ["msstore", "winget"],
            () => [],
            _ => Task.FromCanceled<int?>(new CancellationToken(true)),
            (s, c) => new SourceFailure(s, s, c),
            leftOut.AddRange));

        Assert.Empty(leftOut);
    }

    [Theory] // R2.6
    [InlineData("msstore", "msstore")]
    [InlineData("", "msstore")]
    [InlineData(null, "msstore")]
    public void A_package_from_a_failed_source_names_it(string? source, string expected)
    {
        var error = CatalogConnection.FailedSourceError("Some.App", source, [new SourceFailure("msstore", "'msstore' failed.", Rest)]);

        Assert.NotNull(error);
        Assert.Contains(expected, error.Message);
        Assert.Contains("Some.App", error.Message);
        Assert.Equal(Rest, error.HResult);
    }

    [Fact] // R2.6: a package from a working source really is gone
    public void A_failure_in_another_source_is_not_blamed()
    {
        Assert.Null(CatalogConnection.FailedSourceError("Some.App", "winget", [new SourceFailure("msstore", "failed", Rest)]));
    }
}
