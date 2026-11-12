using CartonStash.Core.Models;
using CartonStash.Core.ViewModels;

namespace CartonStash.Core.Tests;

/// <summary>Spec 009 amendment R4: filter, tabs and sort of the Activity pane.</summary>
public sealed class ActivityArrangerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);

    internal static PackageOperation Op(string id, string name, OperationState state, int sequence, int startedMinute,
        int? finishedMinute = null, OperationKind kind = OperationKind.Upgrade)
    {
        var op = new PackageOperation(kind, Pkg.Installed(id, name), false, _ => { }, _ => { }, _ => { }, () => { })
        {
            Sequence = sequence,
            StartedAt = T0.AddMinutes(startedMinute),
            FinishedAt = finishedMinute is { } f ? T0.AddMinutes(f) : null,
        };
        (op.Phase, op.Result) = state switch
        {
            OperationState.Queued => (OperationPhase.Queued, (OperationResult?)null),
            OperationState.Running => (OperationPhase.Running, null),
            OperationState.Succeeded => (OperationPhase.Done, OperationResult.Succeeded),
            OperationState.RestartRequired => (OperationPhase.Done, OperationResult.SucceededRestartRequired),
            OperationState.Unconfirmed => (OperationPhase.Done, OperationResult.SucceededUnconfirmed),
            OperationState.Cancelled => (OperationPhase.Done, OperationResult.Cancelled),
            _ => (OperationPhase.Done, OperationResult.Failed),
        };
        return op;
    }

    // Like the 2026-11-12 screenshot: one running, queued ones, finished ones.
    private static List<PackageOperation> Sample() =>
    [
        Op("Anaconda.Miniconda3", "Miniconda3", OperationState.Queued, 3, 11),
        Op("ElectronicArts.EADesktop", "EA app", OperationState.Queued, 4, 11),
        Op("OpenJS.NodeJS", "Node.js", OperationState.Running, 2, 25),
        Op("Autodesk.Fusion", "Autodesk Fusion", OperationState.Failed, 1, 9, 20),
        Op("Microsoft.DotNet.DesktopRuntime.8", "Desktop Runtime 8", OperationState.Succeeded, 0, 5, 24),
        Op("Zoom.Zoom", "Zoom", OperationState.Cancelled, 5, 30, 31, OperationKind.Install),
        Op("Git.Git", "Git", OperationState.Unconfirmed, 6, 2, 22),
    ];

    private static string[] Names(ActivityArrangement a) =>
        a.Entries.Select(e => e is ActivityHeader h ? $"[{h.Name} {h.Count}]" : ((PackageOperation)e).Package.Name).ToArray();

    [Fact]
    public void All_tab_groups_active_then_failed_then_done_with_default_order() // R4.4, R4.5
    {
        var a = ActivityArranger.Arrange(Sample(), "", ActivityTab.All, ActivitySort.Default);

        Assert.Equal(
            ["[Active 3]", "Node.js", "Miniconda3", "EA app", "[Failed 1]", "Autodesk Fusion", "[Done 3]", "Zoom", "Desktop Runtime 8", "Git"],
            Names(a));
    }

    [Fact]
    public void Counts_per_tab() // R4.2
    {
        var a = ActivityArranger.Arrange(Sample(), null, ActivityTab.All, ActivitySort.Default);

        Assert.Equal((7, 3, 3, 1), (a.All, a.Active, a.Done, a.Failed));
    }

    [Theory]
    [InlineData(ActivityTab.Active, new[] { "Node.js", "Miniconda3", "EA app" })]
    [InlineData(ActivityTab.Failed, new[] { "Autodesk Fusion" })]
    [InlineData(ActivityTab.Done, new[] { "Zoom", "Desktop Runtime 8", "Git" })]
    public void Other_tabs_show_one_category_without_dividers(ActivityTab tab, string[] expected) // R4.3, R4.4
    {
        Assert.Equal(expected, Names(ActivityArranger.Arrange(Sample(), "", tab, ActivitySort.Default)));
    }

    [Theory]
    [InlineData("node", "Node.js")] // name, any case
    [InlineData("autodesk.", "Autodesk Fusion")] // ID
    [InlineData("  zoom ", "Zoom")] // trimmed
    public void Text_filter_matches_name_or_id(string filter, string expected) // R4.1
    {
        var a = ActivityArranger.Arrange(Sample(), filter, ActivityTab.All, ActivitySort.Default);

        Assert.Equal(expected, Assert.Single(a.Entries.OfType<PackageOperation>()).Package.Name);
    }

    [Fact]
    public void Text_filter_matches_the_action_and_counts_follow_it() // R4.1, R4.2
    {
        var a = ActivityArranger.Arrange(Sample(), "install", ActivityTab.All, ActivitySort.Default);

        Assert.Equal(["[Done 1]", "Zoom"], Names(a)); // empty groups get no divider
        Assert.Equal((1, 0, 1, 0), (a.All, a.Active, a.Done, a.Failed));
    }

    [Fact]
    public void Nothing_matches() // R4.7
    {
        var a = ActivityArranger.Arrange(Sample(), "nope", ActivityTab.All, ActivitySort.Default);

        Assert.Empty(a.Entries);
        Assert.Equal(0, a.All);
    }

    [Fact]
    public void Tab_with_no_match_is_empty_though_others_have_some() // R4.7
    {
        Assert.Empty(ActivityArranger.Arrange(Sample(), "zoom", ActivityTab.Failed, ActivitySort.Default).Entries);
    }

    [Theory]
    [InlineData(ActivitySort.Newest, new[] { "Node.js", "EA app", "Miniconda3" },new[] { "Zoom", "Desktop Runtime 8", "Git" })]
    [InlineData(ActivitySort.Oldest, new[] { "Miniconda3", "EA app", "Node.js" }, new[] { "Git", "Desktop Runtime 8", "Zoom" })]
    [InlineData(ActivitySort.Name, new[] { "EA app", "Miniconda3", "Node.js" }, new[] { "Desktop Runtime 8", "Git", "Zoom" })]
    public void Sort_applies_inside_each_group(ActivitySort sort, string[] active, string[] done) // R4.5
    {
        Assert.Equal(active, Names(ActivityArranger.Arrange(Sample(), "", ActivityTab.Active, sort)));
        Assert.Equal(done, Names(ActivityArranger.Arrange(Sample(), "", ActivityTab.Done, sort)));
        Assert.Equal("[Active 3]", Names(ActivityArranger.Arrange(Sample(), "", ActivityTab.All, sort))[0]); // groups keep their order
    }

    [Fact]
    public void Default_lists_queued_operations_in_run_order_after_the_running_one() // R4.5
    {
        var ops = new List<PackageOperation>
        {
            Op("C", "C", OperationState.Queued, 9, 1),
            Op("A", "A", OperationState.Queued, 7, 1),
            Op("B", "B", OperationState.Running, 8, 3),
        };

        Assert.Equal(["B", "A", "C"], Names(ActivityArranger.Arrange(ops, "", ActivityTab.Active, ActivitySort.Default)));
    }

    [Fact]
    public void Header_reads_its_count_in_words() // R4.10
    {
        Assert.Equal("Failed, 1 operation", new ActivityHeader(ActivityTab.Failed, 1).AccessibleName);
        Assert.Equal("Done, 3 operations", new ActivityHeader(ActivityTab.Done, 3).AccessibleName);
    }
}
