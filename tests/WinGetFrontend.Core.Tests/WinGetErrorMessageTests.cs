using Microsoft.Management.Deployment;
using WinGetFrontend.Core.Services;

namespace WinGetFrontend.Core.Tests;

public class WinGetErrorMessageTests
{
    [Theory] // R3.4: readable messages, never a raw HRESULT
    [InlineData(ConnectResultStatus.CatalogError, "internet connection")]
    [InlineData(ConnectResultStatus.SourceAgreementsNotAccepted, "agreement")]
    public void Connect_failures_are_readable(ConnectResultStatus status, string expected)
    {
        Assert.Contains(expected, WinGetPackageService.DescribeConnectFailure(status));
    }

    [Theory]
    [InlineData(FindPackagesResultStatus.CatalogError, "internet connection")]
    [InlineData(FindPackagesResultStatus.BlockedByPolicy, "policy")]
    [InlineData(FindPackagesResultStatus.InternalError, "InternalError")]
    public void Find_failures_are_readable(FindPackagesResultStatus status, string expected)
    {
        Assert.Contains(expected, WinGetPackageService.DescribeFindFailure(status));
    }
}
