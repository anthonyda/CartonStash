using Microsoft.Management.Deployment;
using CartonStash.Core.Models;
using CartonStash.Core.Services;

namespace CartonStash.Core.Tests;

/// <summary>014 R1.2: a version's direction comes from WinGet's comparer, installed version compared with that one.</summary>
public class VersionDirectionTests
{
    [Theory]
    [InlineData(CompareResult.Greater, VersionDirection.Older)]
    [InlineData(CompareResult.Lesser, VersionDirection.Newer)]
    [InlineData(CompareResult.Equal, VersionDirection.Installed)]
    [InlineData(CompareResult.Unknown, VersionDirection.Unknown)]
    public void Installed_compared_with_the_version(CompareResult installedVsVersion, VersionDirection expected) =>
        Assert.Equal(expected, WinGetPackageService.Direction(installedVsVersion, installed: true, primaryMatch: true));

    [Fact]
    public void Not_installed() =>
        Assert.Equal(VersionDirection.NotInstalled, WinGetPackageService.Direction(null, installed: false, primaryMatch: true));

    [Fact]
    public void A_row_WinGet_did_not_match_itself_claims_no_direction() => // 002 R6.4
        Assert.Equal(VersionDirection.Unknown, WinGetPackageService.Direction(CompareResult.Greater, installed: true, primaryMatch: false));

    [Fact]
    public void Installed_version_is_marked_by_direction_or_by_text()
    {
        Assert.Equal("1.0 (installed)", ViewModels.PackageDetailsViewModel.VersionLabel(new("1.0", VersionDirection.Installed), null));
        Assert.Equal("1.0 (installed)", ViewModels.PackageDetailsViewModel.VersionLabel(new("1.0", VersionDirection.Unknown), "1.0"));
        Assert.Equal("0.9", ViewModels.PackageDetailsViewModel.VersionLabel(new("0.9", VersionDirection.Older), "1.0"));
    }
}

/// <summary>BR-22 (014 R1.2): WinGet's comparer refusing a version makes it Unknown, not a failed load.</summary>
public class SafeCompareTests
{
    [Fact]
    public void A_comparison_that_throws_is_unknown_R1_2()
    {
        var compared = CartonStash.Core.Services.WinGetPackageService.SafeCompare(() => throw new ArgumentException("empty version"));

        Assert.Null(compared);
        Assert.Equal(CartonStash.Core.Models.VersionDirection.Unknown,
            CartonStash.Core.Services.WinGetPackageService.Direction(compared, installed: true, primaryMatch: true));
    }

    [Fact]
    public void A_comparison_that_works_is_kept()
    {
        Assert.Equal(Microsoft.Management.Deployment.CompareResult.Lesser,
            CartonStash.Core.Services.WinGetPackageService.SafeCompare(() => Microsoft.Management.Deployment.CompareResult.Lesser));
    }
}
