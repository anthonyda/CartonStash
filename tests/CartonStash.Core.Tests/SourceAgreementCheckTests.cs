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
        Assert.True(vm.HasNotInUseNotice);

        await vm.ReadAndAcceptCommand.ExecuteAsync(null);

        Assert.Null(vm.NotInUseNotice);
        Assert.False(vm.HasNotInUseNotice);
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
    public async Task Source_that_could_not_be_read_is_not_asked_about() // R3.8
    {
        var app = App(Store(AgreementStatus.Unknown) with { Agreements = [] }, Winget());

        await app.Agreements.RunAsync();

        Assert.Empty(app.Dialogs.SourcePrompts);
        Assert.Empty(app.Agreements.NotAccepted);
        Assert.True(app.Packages.InstalledCalls > 0);
    }

    [Fact]
    public async Task A_declined_source_is_read_only_when_the_user_asks() // R3.8
    {
        var declined = Store(AgreementStatus.Declined) with { Agreements = [] };
        var app = App(declined);
        await app.Agreements.RunAsync();
        Assert.Empty(app.Dialogs.SourcePrompts);

        app.Packages.AgreementsWhenRead = [new Agreement("Terms", "…", null)];
        await app.Agreements.ReadAndAcceptAsync();

        Assert.Single(app.Dialogs.SourcePrompts);
        Assert.Equal([("msstore", true)], app.Packages.AgreementAnswers);
    }

    [Fact]
    public async Task Says_so_when_the_agreements_cannot_be_read_on_request() // R3.8
    {
        var app = App(Store(AgreementStatus.Declined) with { Agreements = [] });
        await app.Agreements.RunAsync();

        await app.Agreements.ReadAndAcceptAsync();

        Assert.Empty(app.Dialogs.SourcePrompts);
        Assert.Empty(app.Packages.AgreementAnswers);
        Assert.Contains("can't be reached", Assert.Single(app.Dialogs.Confirmations));
    }

    [Fact]
    public void Package_from_a_source_not_in_use_says_how_to_use_it() // R3.10
    {
        Assert.Contains("Settings", CatalogConnection.LeftOutSourceError("msstore", ["msstore"])!.Message);
        Assert.Null(CatalogConnection.LeftOutSourceError("winget", ["msstore"]));
        Assert.Null(CatalogConnection.LeftOutSourceError(null, ["msstore"]));
    }
}
