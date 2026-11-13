using CartonStash.Core.Services;

namespace CartonStash.Core.Tests;

/// <summary>018 R1.4 (T9 finding): the permission notification's click opens Activity; the updates one opens Updates.</summary>
public class NotificationArgumentsTests
{
    [Theory]
    [InlineData("page=activity", "activity")]
    [InlineData("page=updates", "updates")]
    [InlineData("other=1;page=activity", "activity")]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("pages=activity", null)]
    public void Reads_the_page_argument(string? argument, string? page)
    {
        Assert.Equal(page, NotificationArguments.Page(argument));
    }
}
