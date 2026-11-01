using CartonStash.Core.Models;
using CartonStash.Core.Services;

namespace CartonStash.Core.Tests;

/// <summary>Spec 010 R3.11–R3.13.</summary>
public sealed class CheckHistoryStoreTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "CartonStashTests", Guid.NewGuid().ToString("N"));
    private readonly TestClock _clock = new() { Now = Now };

    private CheckHistoryStore Store => new(_dir, _clock);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private static CheckRecord Record(DateTimeOffset time, CheckResult result = CheckResult.None) => new(time, CheckTrigger.Schedule, result);

    [Fact]
    public void No_file_is_an_empty_history()
    {
        Assert.Empty(Store.Read());
    }

    [Fact]
    public void Lists_newest_first_and_keeps_every_field() // R3.11, R5.8
    {
        var full = new CheckRecord(Now, CheckTrigger.MissedCheck, CheckResult.Updates, 3, 1, ["Git", "Node.js"]);
        Store.Append(Record(Now.AddHours(-1)));
        Store.Append(full);

        var read = Store.Read();

        Assert.Equal(2, read.Count);
        Assert.Equal((full.Time, full.Trigger, full.Result, full.UpdateCount, full.ExcludedCount), (read[0].Time, read[0].Trigger, read[0].Result, read[0].UpdateCount, read[0].ExcludedCount));
        Assert.Equal(["Git", "Node.js"], read[0].Names!);
    }

    [Fact]
    public void Entries_older_than_7_days_are_deleted_even_with_no_new_check() // R3.12
    {
        Store.Append(Record(Now.AddDays(-6)));
        Store.Append(Record(Now));

        _clock.Now = Now.AddDays(2);

        Assert.Single(Store.Read());
        Assert.DoesNotContain(Now.AddDays(-6).ToString("yyyy-MM-dd"), File.ReadAllText(Store.FilePath));
    }

    [Fact]
    public void A_corrupt_file_is_empty_and_replaced_by_the_next_check()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Store.FilePath, "{ not json");

        Assert.Empty(Store.Read());

        Store.Append(Record(Now));
        Assert.Single(Store.Read());
    }

    [Theory]
    [InlineData(@"Can't open C:\Users\anthony\AppData\file.log now", "Can't open [path] now")]
    [InlineData(@"Failed at \\server\share\x", "Failed at [path]")]
    [InlineData("No network", "No network")]
    public void Reasons_hold_no_paths(string reason, string expected) // R3.13
    {
        Store.Append(new CheckRecord(Now, CheckTrigger.Schedule, CheckResult.Failed, Reason: reason));

        Assert.Equal(expected, Store.Read()[0].Reason);
    }

    [Fact]
    public void A_failed_write_does_not_throw() // R3.13
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_dir)!);
        File.WriteAllText(_dir, "a file where the folder should be");
        try
        {
            Store.Append(Record(Now));
        }
        finally
        {
            File.Delete(_dir);
        }
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; }

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
