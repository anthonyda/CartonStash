using CartonStash.Core.ViewModels;

namespace CartonStash.Core.Tests;

/// <summary>Spec 005 R5 (package icons).</summary>
public class PackageRowIconTests
{
    [Fact]
    public async Task Icon_is_loaded_once_on_request() // R5.1, R5.2
    {
        var app = new TestApp();
        app.Packages.IconUrls["Mozilla.Firefox"] = "https://cdn.winget.microsoft.com/icons/fc73.ico";
        var row = new PackageRowViewModel(Pkg.Remote("Mozilla.Firefox"), app.Actions, RowActions.Install);
        Assert.Empty(app.Packages.IconRequests); // nothing until the row is shown

        await row.EnsureIconAsync();
        await row.EnsureIconAsync();

        Assert.Equal(new Uri("https://cdn.winget.microsoft.com/icons/fc73.ico"), row.IconUri);
        Assert.True(row.HasIcon);
        Assert.Single(app.Packages.IconRequests);
    }

    [Fact]
    public async Task No_icon_leaves_the_glyph() // R5.1
    {
        var app = new TestApp();
        var row = new PackageRowViewModel(Pkg.Remote("Git.Git"), app.Actions, RowActions.Install);

        await row.EnsureIconAsync();

        Assert.False(row.HasIcon);
    }

    [Fact]
    public async Task Http_icon_shows_the_glyph() // R6.1
    {
        var app = new TestApp();
        app.Packages.IconUrls["Plain"] = "http://example.com/icon.png";
        var row = new PackageRowViewModel(Pkg.Remote("Plain"), app.Actions, RowActions.Install);

        await row.EnsureIconAsync();

        Assert.Null(row.IconUri);
        Assert.False(row.HasIcon);
    }

    [Fact]
    public async Task Unmanaged_packages_are_not_asked_and_non_web_urls_are_ignored() // R5.2
    {
        var app = new TestApp();
        app.Packages.IconUrls["Evil"] = "file:///C:/Windows/evil.ico";
        var unmanaged = new PackageRowViewModel(Pkg.Installed("ARP.X", source: ""), app.Actions, RowActions.Uninstall);
        var evil = new PackageRowViewModel(Pkg.Remote("Evil"), app.Actions, RowActions.Install);

        await unmanaged.EnsureIconAsync();
        await evil.EnsureIconAsync();

        Assert.Equal(["Evil"], app.Packages.IconRequests);
        Assert.Null(evil.IconUri);
    }
}
