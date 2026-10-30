using CartonStash.Core.Services;

namespace CartonStash.Core.Tests;

/// <summary>Spec 009 R2.8–R2.10: finding WinGet's record of one operation.</summary>
public sealed class WinGetRecordReaderTests : IDisposable
{
    private const string App = "CartonStash.exe";

    private static readonly DateTimeOffset Start = new(2026, 10, 30, 21, 6, 18, TimeSpan.Zero);
    private static readonly DateTimeOffset End = Start.AddSeconds(2);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "CartonStashTests", Guid.NewGuid().ToString("N"));

    public WinGetRecordReaderTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private WinGetRecord Find() => new WinGetRecordReader(_folder, App).Find(Start, End);

    private string Record(DateTimeOffset created, string caller = App, string body = "installer args")
    {
        var path = Path.Combine(_folder, $"WinGetCOM-{Guid.NewGuid():N}.log");
        File.WriteAllText(path, $"2026-10-30 <I> [CORE] IsCOMCall:1; Caller: {caller}\r\n{body}\r\n");
        File.SetCreationTimeUtc(path, created.UtcDateTime);
        return path;
    }

    [Fact]
    public void The_one_record_of_the_operation_is_found() // R2.8
    {
        Record(Start.AddMilliseconds(150), body: "the right one");

        var record = Find();

        Assert.Contains("the right one", record.Text);
        Assert.Null(record.Missing);
    }

    [Fact]
    public void Times_at_either_edge_count() // R2.8
    {
        Record(Start);

        Assert.NotNull(Find().Text);
    }

    [Fact]
    public void No_record_says_so() // R2.9
    {
        var record = Find();

        Assert.Null(record.Text);
        Assert.Equal(WinGetRecordReader.NotFound, record.Missing);
    }

    [Fact]
    public void A_missing_folder_says_so() // R2.9
    {
        var record = new WinGetRecordReader(Path.Combine(_folder, "nope"), App).Find(Start, End);

        Assert.Equal(WinGetRecordReader.NotFound, record.Missing);
    }

    [Fact]
    public void Several_matches_show_none() // R2.9: never another operation's record
    {
        Record(Start.AddSeconds(1), body: "first");
        Record(Start.AddSeconds(1), body: "second");

        var record = Find();

        Assert.Null(record.Text);
        Assert.Equal(WinGetRecordReader.Ambiguous, record.Missing);
    }

    [Fact]
    public void Another_apps_record_is_ignored() // R2.9
    {
        Record(Start.AddSeconds(1), caller: "winget.exe", body: "not ours");

        Assert.Equal(WinGetRecordReader.NotFound, Find().Missing);
    }

    [Fact]
    public void A_record_with_no_caller_is_ignored() // R2.9
    {
        var path = Path.Combine(_folder, "WinGetCOM-1.log");
        File.WriteAllText(path, "no caller line");
        File.SetCreationTimeUtc(path, Start.AddSeconds(1).UtcDateTime);

        Assert.Equal(WinGetRecordReader.NotFound, Find().Missing);
    }

    [Theory] // R2.9
    [InlineData(-1)]
    [InlineData(3)]
    public void Records_outside_the_operation_are_ignored(int seconds)
    {
        Record(Start.AddSeconds(seconds));

        Assert.Equal(WinGetRecordReader.NotFound, Find().Missing);
    }

    [Fact]
    public void A_file_WinGet_still_has_open_can_be_read() // design
    {
        var path = Record(Start.AddSeconds(1));
        using var open = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);

        Assert.NotNull(Find().Text);
    }

    [Fact]
    public void A_locked_file_says_so_and_never_throws() // R2.5, R2.9
    {
        var path = Record(Start.AddSeconds(1));
        using var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        Assert.Equal(WinGetRecordReader.Unreadable, Find().Missing);
    }

    [Theory] // R2.10
    [InlineData("from url: https://user:hunter2@private.example.com:8443/a/b.exe?sig=abc#part now", "from url: https://private.example.com:8443/a/b.exe now")]
    [InlineData("url: HTTP://example.com/x?token=1", "url: http://example.com/x")]
    [InlineData("'https://example.com/x?y=1'", "'https://example.com/x'")]
    [InlineData("Installer download headers: Authorization=Bearer abc", "Installer download headers: (removed)")]
    [InlineData("Installer download headers: ", "Installer download headers: (removed)")]
    [InlineData("C:\\temp\\a.exe /S", "C:\\temp\\a.exe /S")]
    public void Secrets_are_removed(string line, string expected)
    {
        Assert.Equal(expected, WinGetRecordReader.Clean(line));
    }

    [Fact]
    public void Found_records_are_cleaned() // R2.10
    {
        Record(Start.AddSeconds(1), body: "https://example.com/x?token=hunter2");

        Assert.DoesNotContain("hunter2", Find().Text);
    }
}
