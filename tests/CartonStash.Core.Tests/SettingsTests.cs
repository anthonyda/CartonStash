using Microsoft.Management.Deployment;
using CartonStash.Core.Models;
using CartonStash.Core.Services;
using CartonStash.Core.ViewModels;

namespace CartonStash.Core.Tests;

/// <summary>Spec 006.</summary>
public sealed class SettingsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "CartonStashTests", Guid.NewGuid().ToString("N"));

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

        var saved = new AppSettings { Theme = AppTheme.Dark, InstallScope = InstallScope.User, StartPage = StartPage.Updates };
        store.Save(saved);
        var (loaded, reset) = new JsonSettingsStore(SettingsPath).Load();

        Assert.Equal(saved, loaded);
        Assert.False(reset);
    }

    [Fact]
    public void Window_layout_survives_a_restart() // R6.1, R6.2
    {
        var saved = new AppSettings { Window = new WindowLayout(-1500, 120, 1400, 900, Maximized: true), NavPaneOpen = false };
        new JsonSettingsStore(SettingsPath).Save(saved);

        var (loaded, reset) = new JsonSettingsStore(SettingsPath).Load();

        Assert.Equal(saved, loaded);
        Assert.NotEqual(new AppSettings(), loaded);
        Assert.False(reset);
    }

    [Fact]
    public void Activity_sort_survives_a_restart() // 009 R4.8
    {
        var saved = new AppSettings { ActivitySort = ActivitySort.Oldest };
        new JsonSettingsStore(SettingsPath).Save(saved);

        var (loaded, reset) = new JsonSettingsStore(SettingsPath).Load();

        Assert.Equal(ActivitySort.Oldest, loaded.ActivitySort);
        Assert.NotEqual(new AppSettings(), loaded);
        Assert.False(reset);
    }

    [Fact]
    public void File_from_before_window_layout_keeps_defaults() // R6.4, 009 R4.8
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(SettingsPath, """{ "Theme": "Dark" }""");

        var (loaded, reset) = new JsonSettingsStore(SettingsPath).Load();

        Assert.Null(loaded.Window);
        Assert.True(loaded.NavPaneOpen);
        Assert.Equal(ActivitySort.Default, loaded.ActivitySort);
        Assert.False(reset);
    }

    [Fact]
    public void Source_agreement_answers_survive_a_restart() // R3.9
    {
        var saved = new AppSettings
        {
            SourceAgreementAnswers = new Dictionary<string, SourceAgreementAnswer> { ["StoreEdgeFD"] = new(true, "ABC") },
        };
        new JsonSettingsStore(SettingsPath).Save(saved);

        var (loaded, reset) = new JsonSettingsStore(SettingsPath).Load();

        Assert.Equal(saved, loaded);
        Assert.Equal(new SourceAgreementAnswer(true, "ABC"), loaded.SourceAgreementAnswers["StoreEdgeFD"]);
        Assert.False(reset);
    }

    [Fact]
    public void Changing_an_answer_changes_the_settings() // R3.9
    {
        var accepted = new AppSettings { SourceAgreementAnswers = new Dictionary<string, SourceAgreementAnswer> { ["a"] = new(true, "x") } };
        var declined = new AppSettings { SourceAgreementAnswers = new Dictionary<string, SourceAgreementAnswer> { ["a"] = new(false, "x") } };

        Assert.NotEqual(accepted, declined);
        Assert.NotEqual(new AppSettings(), accepted);
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
    [InlineData("""{ "StartPage": "Purple" }""")]
    [InlineData("""{ "SourceAgreementAnswers": null }""")]
    [InlineData("""{ "CheckMode": "Purple" }""")]
    [InlineData("""{ "CheckIntervalHours": 5 }""")]
    [InlineData("""{ "CheckTime": "soon" }""")]
    [InlineData("""{ "ExcludedPackageIds": null }""")]
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
    public void File_from_before_start_page_opens_on_search_without_a_reset() // R5.4
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(SettingsPath, """{ "Theme": "Dark", "InstallScope": "User" }""");

        var (loaded, reset) = new JsonSettingsStore(SettingsPath).Load();

        Assert.Equal(StartPage.Search, loaded.StartPage);
        Assert.Equal(AppTheme.Dark, loaded.Theme);
        Assert.Empty(loaded.SourceAgreementAnswers); // R3.9: counts as unanswered
        Assert.False(reset);
    }

    [Fact]
    public void File_from_before_background_checks_keeps_them_off_without_a_reset() // 010 R3.5
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(SettingsPath, """{ "Theme": "Dark", "InstallScope": "User", "StartPage": "Updates" }""");

        var (loaded, reset) = new JsonSettingsStore(SettingsPath).Load();

        Assert.Equal(CheckMode.Off, loaded.CheckMode);
        Assert.Equal(new TimeOnly(22, 0), loaded.CheckTime);
        Assert.Empty(loaded.ExcludedPackageIds);
        Assert.False(reset);
    }

    [Fact]
    public void Check_settings_and_exclusions_round_trip() // 010 R3.1, R5.6
    {
        var store = new JsonSettingsStore(SettingsPath);

        var saved = new AppSettings
        {
            CheckMode = CheckMode.DailyTime,
            CheckIntervalHours = 168,
            CheckTime = new TimeOnly(6, 30),
            ExcludedPackageIds = ["Git.Git", "Mozilla.Firefox"],
        };
        store.Save(saved);

        Assert.Equal(saved, new JsonSettingsStore(SettingsPath).Load().Settings);
    }

    [Fact]
    public void An_exclusion_matches_the_id_in_any_case() // 010 R5.3
    {
        var settings = new AppSettings { ExcludedPackageIds = ["Git.Git"] };

        Assert.True(settings.IsExcluded("git.git"));
        Assert.False(settings.IsExcluded("Git.Lfs"));
    }

    [Fact]
    public void Changing_the_exclusions_counts_as_a_change_and_an_equal_list_does_not() // 010 R5.4
    {
        var service = new SettingsService(new JsonSettingsStore(SettingsPath));
        var changed = 0;
        service.Changed += (_, _) => changed++;

        service.Update(s => s with { ExcludedPackageIds = ["Git.Git"] });
        service.Update(s => s with { ExcludedPackageIds = ["Git.Git"] }); // no-op

        Assert.Equal(1, changed);
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

    [Fact]
    public void Packaged_app_copies_the_portable_settings_once() // 007 R4.1
    {
        var localAppData = Path.Combine(_dir, "local");
        var package = Path.Combine(_dir, "package");
        Directory.CreateDirectory(Path.Combine(localAppData, "CartonStash"));
        File.WriteAllText(Path.Combine(localAppData, "CartonStash", "settings.json"), """{ "Theme": "Dark" }""");

        var path = JsonSettingsStore.DefaultPath(package, localAppData);

        Assert.Equal(Path.Combine(package, "settings.json"), path);
        Assert.Equal(AppTheme.Dark, new JsonSettingsStore(path).Load().Settings.Theme);

        // Later changes in the package are kept: no second copy.
        new JsonSettingsStore(path).Save(new AppSettings { Theme = AppTheme.Light });
        JsonSettingsStore.DefaultPath(package, localAppData);
        Assert.Equal(AppTheme.Light, new JsonSettingsStore(path).Load().Settings.Theme);
    }

    [Fact]
    public void Packaged_app_falls_back_to_the_pre_rename_settings() // 007 R4.1 + rename
    {
        var localAppData = Path.Combine(_dir, "local");
        Directory.CreateDirectory(Path.Combine(localAppData, "WinGetFrontend"));
        File.WriteAllText(Path.Combine(localAppData, "WinGetFrontend", "settings.json"), """{ "InstallScope": "User" }""");

        var path = JsonSettingsStore.DefaultPath(Path.Combine(_dir, "package"), localAppData);

        Assert.Equal(InstallScope.User, new JsonSettingsStore(path).Load().Settings.InstallScope);
    }

    [Fact]
    public void Unpackaged_app_keeps_settings_in_local_app_data() // 006, portable zip
    {
        var localAppData = Path.Combine(_dir, "local");

        var path = JsonSettingsStore.DefaultPath(packageFolder: null, localAppData);

        Assert.Equal(Path.Combine(localAppData, "CartonStash", "settings.json"), path);
        Assert.False(AppPackage.IsPackaged); // test host is not packaged
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
        var vm = Vm(settings, new FakePackageService(), new FakeDialogs(), new FakeClipboard());
        Assert.False(vm.ShowScopeWarning);

        vm.ThemeIndex = (int)AppTheme.Dark;
        vm.ScopeIndex = (int)InstallScope.System;

        Assert.Equal(AppTheme.Dark, settings.Current.Theme);
        Assert.Equal(InstallScope.System, settings.Current.InstallScope);
        Assert.True(vm.ShowScopeWarning);
    }

    [Fact]
    public void View_model_shows_and_writes_the_start_page() // R5.1, R5.3
    {
        var settings = new FakeSettings(new AppSettings { StartPage = StartPage.Installed });
        var vm = Vm(settings, new FakePackageService(), new FakeDialogs(), new FakeClipboard());
        Assert.Equal((int)StartPage.Installed, vm.StartPageIndex);

        vm.StartPageIndex = (int)StartPage.Updates;
        Assert.Equal(StartPage.Updates, settings.Current.StartPage);

        vm.StartPageIndex = -1; // ComboBox with no selection: ignored
        Assert.Equal(StartPage.Updates, settings.Current.StartPage);
    }

    [Fact]
    public async Task Refresh_sources_refreshes_each_and_reports_failures_per_source() // R3.2
    {
        var packages = new FakePackageService();
        packages.Sources.Add(Source("winget"));
        packages.Sources.Add(Source("msstore"));
        packages.RefreshErrors["msstore"] = "Couldn't reach the source.";
        var vm = Vm(new FakeSettings(), packages, new FakeDialogs(), new FakeClipboard());
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
        var dialogs = new FakeDialogs { SourceAnswer = AgreementAnswer.Declined };
        var vm = Vm(new FakeSettings(), packages, dialogs, new FakeClipboard());
        var msstore = Source("msstore", new Agreement("Terms of Transaction", "…", "https://aka.ms/microsoft-store-terms-of-transaction"));

        await vm.ReviewAgreementsCommand.ExecuteAsync(msstore);
        Assert.Equal([("msstore", false)], packages.AgreementAnswers); // R3.8: a decline is saved too

        dialogs.SourceAnswer = AgreementAnswer.Dismissed;
        await vm.ReviewAgreementsCommand.ExecuteAsync(msstore);
        Assert.Single(packages.AgreementAnswers); // R3.9
        dialogs.SourceAnswer = AgreementAnswer.Accepted;

        dialogs.Answer = true;
        await vm.ReviewAgreementsCommand.ExecuteAsync(msstore);
        Assert.Equal(("msstore", true), packages.AgreementAnswers[^1]);
    }

    [Fact]
    public async Task Diagnostic_text_has_versions_os_and_sources() // R4.1, R4.2
    {
        var packages = new FakePackageService();
        packages.Sources.Add(Source("winget"));
        var clipboard = new FakeClipboard();
        var vm = Vm(new FakeSettings(), packages, new FakeDialogs(), clipboard);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.CopyDiagnosticsCommand.Execute(null);

        Assert.NotNull(clipboard.Text);
        Assert.Contains("CartonStash: ", clipboard.Text);
        Assert.Contains("WinGet: 1.29.380", clipboard.Text);
        Assert.Contains("OS: ", clipboard.Text);
        Assert.Contains("  winget | Microsoft.PreIndexed.Package | https://cdn.winget.microsoft.com/cache | Trusted", clipboard.Text);
        Assert.Contains("WinGet 1.29.380", vm.VersionText);
    }

    [Theory] // R4.3
    [InlineData("https://cdn.winget.microsoft.com/cache", "https://cdn.winget.microsoft.com/cache")]
    [InlineData("https://user:secret@private.example.com:8443/api?token=abc#part", "https://private.example.com:8443/api")]
    [InlineData(@"C:\Users\someone\my-source", "(local source)")]
    [InlineData(@"\\server\share\source", "(local source)")]
    [InlineData("not a url", "(local source)")]
    public void Diagnostics_remove_secrets_from_source_addresses(string address, string expected)
    {
        Assert.Equal(expected, SettingsViewModel.SafeSourceAddress(address));
    }

    [Fact]
    public async Task Copied_diagnostics_hold_no_source_secrets() // R4.3
    {
        var packages = new FakePackageService();
        packages.Sources.Add(Source("private") with { Argument = "https://user:hunter2@private.example.com/api?token=abc" });
        var clipboard = new FakeClipboard();
        var vm = Vm(new FakeSettings(), packages, new FakeDialogs(), clipboard);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.CopyDiagnosticsCommand.Execute(null);

        Assert.Contains("https://private.example.com/api", clipboard.Text);
        Assert.DoesNotContain("hunter2", clipboard.Text);
        Assert.DoesNotContain("token", clipboard.Text);
    }

    [Fact]
    public async Task Withdrawing_saves_a_decline_and_refresh_skips_the_source() // R3.8, R3.11
    {
        var packages = new FakePackageService();
        var accepted = Source("msstore", new Agreement("Terms", "…", null)) with { AgreementStatus = AgreementStatus.Accepted };
        packages.Sources.Add(accepted);
        packages.Sources.Add(Source("winget"));
        var dialogs = new FakeDialogs();
        var vm = Vm(new FakeSettings(), packages, dialogs, new FakeClipboard());
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.ReviewAgreementsCommand.ExecuteAsync(accepted);
        Assert.Equal([("msstore", false)], packages.AgreementAnswers);
        Assert.Empty(dialogs.SourcePrompts);

        await vm.RefreshSourcesCommand.ExecuteAsync(null);
        Assert.Equal(["winget"], packages.Refreshed);
    }

    [Fact]
    public void Button_offers_what_the_source_needs() // R3.10, R3.11
    {
        Assert.Equal("Read and accept", Source("a").AgreementButtonText);
        Assert.Equal("Withdraw", (Source("a") with { AgreementStatus = AgreementStatus.Accepted }).AgreementButtonText);
    }

    private static SettingsViewModel Vm(ISettings settings, FakePackageService packages, FakeDialogs dialogs, IClipboardService clipboard)
    {
        var queue = new PackageOperationQueue(new ScriptedInstaller());
        return new SettingsViewModel(settings, packages, new SourceAgreementCheck(packages, dialogs, new InstalledPackagesStore(packages, queue)), clipboard);
    }

    private static SourceInfo Source(string name, params Agreement[] agreements) =>
        new(name, name, "Microsoft.PreIndexed.Package", "https://cdn.winget.microsoft.com/cache", true, false, DateTimeOffset.Now, agreements);
}

/// <summary>006 R3.14 to R3.17 (BR-01): explicit sources.</summary>
public class ExplicitSourceTests
{
    [Theory]
    [InlineData(false, false, false, true)]  // not explicit: always
    [InlineData(false, true, false, true)]
    [InlineData(true, false, false, false)]  // explicit, box off: never (R3.14)
    [InlineData(true, true, false, false)]
    [InlineData(true, false, true, true)]    // box on: search only (R3.15)
    [InlineData(true, true, true, false)]    // installed list, updates, background checks: never
    public void An_explicit_source_counts_only_for_search_with_its_box_R3_14_R3_15(bool isExplicit, bool localFirst, bool searched, bool counts)
    {
        var settings = new AppSettings().WithSearched("winget-font", searched);

        Assert.Equal(counts, CartonStash.Core.Services.CatalogConnection.Counts(isExplicit, "winget-font", localFirst, settings));
    }

    [Fact] // R3.17: per source ID, case-insensitive; unchecking removes it
    public void The_box_is_saved_per_source_ID_R3_17()
    {
        var on = new AppSettings().WithSearched("winget-font", true).WithSearched("WINGET-FONT", true);
        Assert.Equal(["winget-font"], on.SearchedExplicitSources);
        Assert.True(on.IsSearched("Winget-Font"));
        Assert.Empty(on.WithSearched("winget-font", false).SearchedExplicitSources);
        Assert.NotEqual(new AppSettings(), on); // a change is saved
    }

    [Fact] // older files: every box off, no reset (as R5.4)
    public void A_file_without_the_setting_leaves_explicit_sources_out_R3_14()
    {
        var settings = System.Text.Json.JsonSerializer.Deserialize<AppSettings>("""{ "Theme": "Dark" }""",
            new System.Text.Json.JsonSerializerOptions { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } })!;

        Assert.Empty(settings.SearchedExplicitSources);
    }

    [Fact]
    public void A_source_that_isnt_explicit_is_always_used_in_searches_R3_15()
    {
        Assert.True(new SourceInfo("winget", "winget", "t", "a", true, false, null, []).UsedInSearches);
        Assert.False(new SourceInfo("winget-font", "winget-font", "t", "a", true, true, null, []).UsedInSearches);
        Assert.Equal("t · a · explicit", new SourceInfo("winget-font", "winget-font", "t", "a", true, true, null, []).DetailText);
    }

    [Fact] // R3.15, R3.16, R3.17
    public async Task Checking_the_box_saves_it_reconnects_and_refresh_follows_it_R3_15_R3_17()
    {
        var app = new TestApp();
        SourceInfo Font() => new("winget-font", "winget-font", "t", "a", true, true, null, []) { UsedInSearches = app.Settings.Current.IsSearched("winget-font") };
        app.Packages.Sources.AddRange([new("winget", "winget", "t", "a", true, false, null, []), Font()]);
        var vm = new SettingsViewModel(app.Settings, app.Packages, app.Agreements, new FakeClipboard());
        await vm.LoadCommand.ExecuteAsync(null);
        Assert.True(vm.HasExplicitSources);

        await vm.RefreshSourcesCommand.ExecuteAsync(null);
        Assert.DoesNotContain("winget-font", app.Packages.Refreshed);

        var invalidations = app.Packages.Invalidations;
        await vm.SetUsedInSearchesAsync(vm.Sources[1], true);
        Assert.True(app.Settings.Current.IsSearched("winget-font"));
        Assert.True(app.Packages.Invalidations > invalidations);
        app.Packages.Sources[1] = Font(); // as WinGetPackageService reads it now
        await vm.LoadCommand.ExecuteAsync(null);
        Assert.True(vm.Sources[1].UsedInSearches);

        await vm.RefreshSourcesCommand.ExecuteAsync(null);
        Assert.Contains("winget-font", app.Packages.Refreshed);
    }
}
