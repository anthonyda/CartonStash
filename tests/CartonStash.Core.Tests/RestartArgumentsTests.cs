using CartonStash.Core.Services;

namespace CartonStash.Core.Tests;

public class RestartArgumentsTests
{
    [Fact]
    public void Round_trips_through_the_command_line() // 004 R6.3, R6.4
    {
        var original = new RestartArguments(6676, [6116, 7020], "1.29.379.0", "updates");

        var parsed = RestartArguments.Parse(original.Format().Split(' '));

        Assert.NotNull(parsed);
        Assert.Equal(6676, parsed.AppProcessId);
        Assert.Equal([6116, 7020], parsed.ServerProcessIds);
        Assert.Equal("1.29.379.0", parsed.PendingVersion);
        Assert.Equal("updates", parsed.Page);
    }

    [Fact]
    public void Missing_parts_round_trip_as_empty()
    {
        var parsed = RestartArguments.Parse(new RestartArguments(1, [], null, null).Format().Split(' '));

        Assert.NotNull(parsed);
        Assert.Empty(parsed.ServerProcessIds);
        Assert.Null(parsed.PendingVersion);
        Assert.Null(parsed.Page);
    }

    [Theory] // a normal start never waits
    [InlineData("")]
    [InlineData("-RegisterForBGTaskServer")]
    [InlineData("--finish-winget-update")]
    [InlineData("--finish-winget-update abc -")]
    [InlineData("--finish-winget-update 12 3,x")]
    public void Anything_else_is_not_a_restart(string line) =>
        Assert.Null(RestartArguments.Parse(line.Split(' ', StringSplitOptions.RemoveEmptyEntries)));
}
