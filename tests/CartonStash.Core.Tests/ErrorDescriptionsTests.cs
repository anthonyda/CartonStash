using CartonStash.Core.Services;

namespace CartonStash.Core.Tests;

public class ErrorDescriptionsTests
{
    [Theory] // 013 R1.1: Windows' codes have a description
    [InlineData(unchecked((int)0x80070005))] // Access is denied.
    [InlineData(unchecked((int)0x800401F5))] // Application not found
    [InlineData(unchecked((int)0x80070002))] // The system cannot find the file specified.
    public void Windows_codes_have_a_description(int hresult)
    {
        var text = ErrorDescriptions.Describe(hresult);

        Assert.False(string.IsNullOrWhiteSpace(text));
        Assert.DoesNotContain('\r', text);
        Assert.DoesNotContain('\n', text);
    }

    [Theory] // 013 R1.1: WinGet's own codes show as hex only
    [InlineData(unchecked((int)0x8A150114))]
    [InlineData(unchecked((int)0x8A150101))]
    public void WinGet_codes_have_none(int hresult) => Assert.Null(ErrorDescriptions.Describe(hresult));
}
