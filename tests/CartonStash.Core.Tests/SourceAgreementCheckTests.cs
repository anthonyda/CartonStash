using CartonStash.Core.Models;
using CartonStash.Core.Services;
using CartonStash.Core.ViewModels;

namespace CartonStash.Core.Tests;

/// <summary>Spec 006 amendment R3.4: the check at start and the Read and accept button.</summary>
public class SourceAgreementCheckTests
{
    private static SourceInfo Store(AgreementStatus status) =>
        new("StoreEdgeFD", "msstore", "Microsoft.Rest", "https://example.com", true, false, null, [new Agreement("Terms", "…", null)], status);

    private static SourceInfo Winget() =>
        new("winget-id", "winget", "Microsoft.PreIndexed.Package", "https://example.com", true, false, null, []);

    private static TestApp App(params SourceInfo[] sources)
    {
        var app = new TestApp();
        app.Packages.Sources.AddRange(sources);
        return app;
    }

    [Fact]
    public async Task Asks_about_an_unanswered_source_before_loading_the_installed_list() // R3.4, R3.5, R3.6
    {
        var app = App(Store(AgreementStatus.Unanswered), Winget());
        var callsWhenAsked = -1;
        app.Dialogs.SourceAsked = () => callsWhenAsked = app.Packages.InstalledCalls;

        await app.Agreements.RunAsync();

        Assert.Equal(0, callsWhenAsked);
        var prompt = Assert.Single(app.Dialogs.SourcePrompts);
        Assert.Equal("msstore", prompt.Source);
        Assert.Contains("region", prompt.Receives);
        Assert.True(app.Packages.InstalledCalls > 0);
    }

    [Theory]
    [InlineData(AgreementStatus.Accepted)]
    [InlineData(AgreementStatus.Declined)]
    public async Task Does_not_ask_after_an_answer(AgreementStatus status) // R3.9
    {
        var app = App(Store(status), Winget());

        await app.Agreements.RunAsync();

        Assert.Empty(app.Dialogs.SourcePrompts);
    }

    [Fact]
    public async Task Accepting_clears_the_list_and_reloads_with_the_source() // R3.8
    {
        var app = App(Store(AgreementStatus.Unanswered));

        await app.Agreements.RunAsync();

        Assert.Equal([("msstore", true)], app.Packages.AgreementAnswers);
        Assert.Empty(app.Agreements.NotAccepted);
        Assert.Equal(1, app.Packages.Invalidations);
    }

    [Fact]
    public async Task Declining_keeps_the_source_listed() // R3.8, R3.10
    {
        var app = App(Store(AgreementStatus.Unanswered));
        app.Dialogs.SourceAnswer = AgreementAnswer.Declined;

        await app.Agreements.RunAsync();

        Assert.Equal([("msstore", false)], app.Packages.AgreementAnswers);
        Assert.Single(app.Agreements.NotAccepted);
    }

    [Fact]
    public async Task Dismissing_saves_nothing_so_it_asks_again() // R3.9
    {
        var app = App(Store(AgreementStatus.Unanswered));
        app.Dialogs.SourceAnswer = AgreementAnswer.Dismissed;

        await app.Agreements.RunAsync();

        Assert.Empty(app.Packages.AgreementAnswers);
        Assert.Single(app.Agreements.NotAccepted);
    }

    [Fact]
    public async Task Search_names_the_source_and_searches_again_after_accepting() // R3.10
    {
        var app = App(Store(AgreementStatus.Declined), Winget());
        var searches = 0;
        app.Packages.Search = (_, _) =>
        {
            searches++;
            return Task.FromResult<IReadOnlyList<PackageInfo>>([]);
        };
        var vm = new SearchViewModel(app.Packages, app.Actions, app.Agreements) { Query = "git" };
        await app.Agreements.RunAsync();
        Assert.Contains("msstore", vm.NotInUseNotice);

        await vm.ReadAndAcceptCommand.ExecuteAsync(null);

        Assert.Null(vm.NotInUseNotice);
        Assert.Equal(1, searches);
    }

    [Fact]
    public async Task Search_shows_the_notice_when_every_source_is_left_out() // R3.10
    {
        var app = App(Store(AgreementStatus.Declined));
        var vm = new SearchViewModel(app.Packages, app.Actions, app.Agreements) { Query = "git" };
        await app.Agreements.RunAsync();

        await vm.SearchCommand.ExecuteAsync(null);

        Assert.Empty(vm.Results);
        Assert.NotNull(vm.NotInUseNotice);
    }

    [Fact]
    public void Package_from_a_source_not_in_use_says_how_to_use_it() // R3.10
    {
        Assert.Contains("Settings", CatalogConnection.LeftOutSourceError("msstore", ["msstore"])!.Message);
        Assert.Null(CatalogConnection.LeftOutSourceError("winget", ["msstore"]));
        Assert.Null(CatalogConnection.LeftOutSourceError(null, ["msstore"]));
    }
}
