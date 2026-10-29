using Microsoft.Management.Deployment;
using WinGetFrontend.Core.Models;
using WinGetFrontend.Core.Services;
using WinGetFrontend.Core.ViewModels;

namespace WinGetFrontend.Core.Tests;

/// <summary>Spec 006.</summary>
public sealed class SettingsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "WinGetFrontendTests", Guid.NewGuid().ToString("N"));

    private string SettingsPath => Path.Combine(_dir, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void Round_trip() // R1.2
    {
        var store = new JsonSettingsStore(SettingsPath);

        store.Save(new AppSettings { Theme = AppTheme.Dark, InstallScope = InstallScope.User });
        var (loaded, reset) = new JsonSettingsStore(SettingsPath).Load();

        Assert.Equal(new AppSettings { Theme = AppTheme.Dark, InstallScope = InstallScope.User }, loaded);
        Assert.False(reset);
    }

    [Fact]
    public void Missing_file_gives_defaults()
    {
        var (loaded, reset) = new JsonSettingsStore(SettingsPath).Load();

        Assert.Equal(new AppSettings(), loaded);
        Assert.False(reset);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("""{ "Theme": "Purple" }""")]
    [InlineData("""{ "Theme": 42 }""")]
    [InlineData("null")]
    public void Corrupt_file_gives_defaults_and_keeps_a_backup(string content)
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(SettingsPath, content);

        var (loaded, reset) = new JsonSettingsStore(SettingsPath).Load();

        Assert.Equal(new AppSettings(), loaded);
        Assert.True(reset);
        Assert.Equal(content, File.ReadAllText(SettingsPath + ".bak"));
    }

    [Fact]
    public void Service_saves_every_change_and_raises_changed() // R1.1, R1.2
    {
        var service = new SettingsService(new JsonSettingsStore(SettingsPath));
        var changed = 0;
        service.Changed += (_, _) => changed++;

        service.Update(s => s with { Theme = AppTheme.Light });
        service.Update(s => s with { Theme = AppTheme.Light }); // no-op

        Assert.Equal(1, changed);
        Assert.Equal(AppTheme.Light, new JsonSettingsStore(SettingsPath).Load().Settings.Theme);
    }

    [Theory] // R2.1: the setting maps onto WinGet's install scope
    [InlineData(InstallScope.Any, PackageInstallScope.Any)]
    [InlineData(InstallScope.User, PackageInstallScope.User)]
    [InlineData(InstallScope.System, PackageInstallScope.System)]
    public void Scope_maps_to_winget(InstallScope scope, PackageInstallScope expected)
    {
        Assert.Equal(expected, WinGetPackageInstaller.ToWinGet(scope));
    }

    [Fact]
    public void View_model_writes_theme_and_scope() // R1.1, R2.1, R2.2
    {
        var settings = new FakeSettings();
        var vm = new SettingsViewModel(settings, new FakePackageService(), new FakeDialogs(), new FakeClipboard());
        Assert.False(vm.ShowScopeWarning);

        vm.ThemeIndex = (int)AppTheme.Dark;
        vm.ScopeIndex = (int)InstallScope.System;

        Assert.Equal(AppTheme.Dark, settings.Current.Theme);
        Assert.Equal(InstallScope.System, settings.Current.InstallScope);
        Assert.True(vm.ShowScopeWarning);
    }

    [Fact]
    public async Task Refresh_sources_refreshes_each_and_reports_failures_per_source() // R3.2
    {
        var packages = new FakePackageService();
        packages.Sources.Add(Source("winget"));
        packages.Sources.Add(Source("msstore"));
        packages.RefreshErrors["msstore"] = "Couldn't reach the source.";
        var vm = new SettingsViewModel(new FakeSettings(), packages, new FakeDialogs(), new FakeClipboard());
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.RefreshSourcesCommand.ExecuteAsync(null);

        Assert.Equal(["winget", "msstore"], packages.Refreshed);
        Assert.Equal("msstore: Couldn't reach the source.", vm.SourcesError);
        Assert.True(packages.Invalidations > 0);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Source_agreements_are_accepted_only_when_the_user_accepts() // R3.3
    {
        var packages = new FakePackageService();
        var dialogs = new FakeDialogs { Answer = false };
        var vm = new SettingsViewModel(new FakeSettings(), packages, dialogs, new FakeClipboard());
        var msstore = Source("msstore", new Agreement("Terms of Transaction", "…", "https://aka.ms/microsoft-store-terms-of-transaction"));

        await vm.ReviewAgreementsCommand.ExecuteAsync(msstore);
        Assert.Empty(packages.AcceptedAgreements);

        dialogs.Answer = true;
        await vm.ReviewAgreementsCommand.ExecuteAsync(msstore);
        Assert.Equal(["msstore"], packages.AcceptedAgreements);
    }

    [Fact]
    public async Task Diagnostic_text_has_versions_os_and_sources() // R4.1, R4.2
    {
        var packages = new FakePackageService();
        packages.Sources.Add(Source("winget"));
        var clipboard = new FakeClipboard();
        var vm = new SettingsViewModel(new FakeSettings(), packages, new FakeDialogs(), clipboard);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.CopyDiagnosticsCommand.Execute(null);

        Assert.NotNull(clipboard.Text);
        Assert.Contains("WinGet Frontend: ", clipboard.Text);
        Assert.Contains("WinGet: 1.29.380", clipboard.Text);
        Assert.Contains("OS: ", clipboard.Text);
        Assert.Contains("  winget | Microsoft.PreIndexed.Package | https://cdn.winget.microsoft.com/cache | Trusted", clipboard.Text);
        Assert.Contains("WinGet 1.29.380", vm.VersionText);
    }

    private static SourceInfo Source(string name, params Agreement[] agreements) =>
        new(name, "Microsoft.PreIndexed.Package", "https://cdn.winget.microsoft.com/cache", true, false, DateTimeOffset.Now, agreements);
}
