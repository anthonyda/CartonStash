using CartonStash.Core.Models;
using CartonStash.Core.Services;
using CartonStash.Core.ViewModels;
using Microsoft.Management.Deployment;

namespace CartonStash.Core.Tests;

/// <summary>Spec 002 amendment R7: version badge colors.</summary>
public class VersionBadgeTests
{
    private static PackageRowViewModel Row(PackageInfo package) => new(package, new TestApp().Actions, RowActions.All);

    private static PackageInfo Installed(string installed, string? available, VersionStatus status, bool update = false, string source = "winget") =>
        new("Chatterino.Chatterino", "Chatterino", installed, available, source, update) { VersionStatus = status };

    [Theory] // R7.1: WinGet's comparison of the source's version with the installed one
    [InlineData(CompareResult.Equal, VersionStatus.Same)]
    [InlineData(CompareResult.Greater, VersionStatus.Older)]
    [InlineData(CompareResult.Lesser, VersionStatus.Newer)]
    [InlineData(CompareResult.Unknown, VersionStatus.Unknown)]
    public void WinGet_comparison_maps_to_status(CompareResult result, VersionStatus expected)
    {
        Assert.Equal(expected, WinGetPackageService.ToVersionStatus(result));
    }

    [Fact]
    public void Same_version_is_green() // R7.1
    {
        var row = Row(Installed("2.56.0.3", "2.56.0.3", VersionStatus.Same));

        Assert.Equal(VersionBadge.Same, row.VersionBadge);
        Assert.Equal("Installed 2.56.0.3, same as winget", row.VersionStatusText);
    }

    [Fact]
    public void Older_with_an_offered_update_is_amber() // R7.1
    {
        var row = Row(Installed("1.0", "2.0", VersionStatus.Older, update: true));

        Assert.Equal(VersionBadge.UpdateOffered, row.VersionBadge);
        Assert.Equal("Installed 1.0, update to 2.0 available from winget", row.VersionStatusText);
    }

    [Fact]
    public void Newer_than_the_source_is_blue() // R7.1, the Chatterino case
    {
        var row = Row(Installed("2.5.6", "2.5.5", VersionStatus.Newer));

        Assert.Equal(VersionBadge.Newer, row.VersionBadge);
        Assert.Equal("Installed 2.5.6, newer than winget's 2.5.5", row.VersionStatusText);
        Assert.Equal("Installed 2.5.6", row.VersionBadgeText);
    }

    [Theory] // R7.1: different, but no update offered or not comparable → neutral
    [InlineData(VersionStatus.Older, false)]
    [InlineData(VersionStatus.Unknown, false)]
    [InlineData(VersionStatus.Unknown, true)]
    public void Uncertain_cases_are_neutral(VersionStatus status, bool update)
    {
        var row = Row(Installed("py312_24.11.1-0", "py314_26.9.4-0", status, update));

        Assert.Equal(VersionBadge.Neutral, row.VersionBadge);
        Assert.Equal("Installed py312_24.11.1-0, winget has py314_26.9.4-0", row.VersionStatusText);
    }

    [Fact]
    public void Unmanaged_programs_stay_neutral() // R7.2
    {
        var row = Row(new PackageInfo(@"ARP\Machine\X64\Steam App 1", "Game", "Unknown", null, "") { VersionStatus = VersionStatus.Same });

        Assert.Equal(VersionBadge.Neutral, row.VersionBadge);
        Assert.Equal("Installed Unknown", row.VersionStatusText);
    }

    [Fact]
    public void Only_wingets_own_match_is_compared() // R7.1, R7.2 (amended): two WSL rows
    {
        var compared = new List<string>();
        var rows = InstalledJoin.Join(
            [new InstalledEntry("ARP-old", "WSL Update", "5.10.102.1", ["{f8474a47}"]), new InstalledEntry("MSIX-new", "WSL", "3.0.4.0", ["wsl_pfn"])],
            [new MatchedPackage("Microsoft.WSL", "WSL", "ARP-old", "5.10.102.1", "2.7.14", "winget", false, ["wsl_pfn"])],
            (_, installed) =>
            {
                compared.Add(installed);
                return VersionStatus.Newer;
            });

        Assert.Equal(["5.10.102.1"], compared);
        Assert.Equal(VersionStatus.Newer, rows.Single(r => r.IsPrimaryMatch).VersionStatus);
        Assert.Equal(VersionStatus.Unknown, rows.Single(r => !r.IsPrimaryMatch).VersionStatus);
    }

    [Fact]
    public void Manifest_matched_rows_stay_neutral() // R7.2 (amended)
    {
        var row = Row(new PackageInfo("Microsoft.WSL", "WSL", "3.0.4.0", "2.7.14", "winget") { IsPrimaryMatch = false });

        Assert.Equal(VersionBadge.Neutral, row.VersionBadge);
        Assert.Equal("Installed 3.0.4.0, winget has 2.7.14", row.VersionStatusText);
    }
}
