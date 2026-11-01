using CartonStash.Core.Models;
using CartonStash.Core.Services;

namespace CartonStash.Core.Tests;

public class SourceFailureMessagesTests
{
    [Theory] // R2.2
    [InlineData(0x80072EFDu)] // WinHTTP: can't connect, seen with the Store blocked
    [InlineData(0x80072EE7u)] // name not resolved
    public void Network_errors_say_the_source_cannot_be_reached(uint code)
    {
        var message = SourceFailureMessages.Describe("msstore", unchecked((int)code), regionIsCountry: true);

        Assert.Contains("'msstore' can't be reached", message);
    }

    [Theory] // R2.3
    [InlineData(0x8A15003Bu)] // REST internal error, with a real region
    [InlineData(0x8A150044u)] // REST endpoint not found
    [InlineData(0x80190194u)] // HTTP 404
    public void A_source_that_answers_with_an_error_is_not_blamed_on_the_connection(uint code)
    {
        var message = SourceFailureMessages.Describe("msstore", unchecked((int)code), regionIsCountry: true);

        Assert.Contains("'msstore' reported a problem", message);
        Assert.DoesNotContain("internet", message);
    }

    [Fact] // R2.4
    public void Without_a_country_the_rest_error_suggests_setting_one()
    {
        var message = SourceFailureMessages.Describe("msstore", unchecked((int)0x8A15003B), regionIsCountry: false);

        Assert.Contains("Language & region", message);
    }

    [Fact] // R2.4: the region only explains this one error
    public void Without_a_country_network_errors_stay_network_errors()
    {
        var message = SourceFailureMessages.Describe("msstore", unchecked((int)0x80072EFD), regionIsCountry: false);

        Assert.Contains("can't be reached", message);
    }

    [Fact]
    public void Unknown_codes_stay_neutral()
    {
        var message = SourceFailureMessages.Describe("msstore", unchecked((int)0x80004005), regionIsCountry: true);

        Assert.Equal("'msstore' failed (WinGet reported an error).", message);
    }

    [Theory] // R2.7
    [InlineData(0x8A15003Bu, true)]
    [InlineData(0x8A15003Bu, false)]
    [InlineData(0x80072EFDu, true)]
    [InlineData(0x80004005u, true)]
    public void Messages_never_show_a_raw_code(uint code, bool regionIsCountry)
    {
        Assert.DoesNotContain("0x", SourceFailureMessages.Describe("msstore", unchecked((int)code), regionIsCountry));
    }

    [Fact] // R2.1
    public void Notice_lists_every_reason()
    {
        var notice = SourceFailureMessages.Notice([new SourceFailure("a", "'a' failed.", 0), new SourceFailure("b", "'b' failed.", 0)]);

        Assert.Equal("Results don't include: 'a' failed. 'b' failed.", notice);
    }

    [Fact]
    public void No_failures_means_no_notice()
    {
        Assert.Null(SourceFailureMessages.Notice([]));
    }

    [Theory] // R2.4: GetUserDefaultGeoName returned 001 for World in Windows Sandbox
    [InlineData("US", true)]
    [InlineData("FR", true)]
    [InlineData("001", false)]
    [InlineData("", false)]
    public void Only_two_letters_are_a_country(string name, bool expected)
    {
        Assert.Equal(expected, WindowsRegion.IsCountryCode(name));
    }
}
