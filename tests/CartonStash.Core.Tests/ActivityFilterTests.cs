using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CartonStash.Core.Models;
using CartonStash.Core.Services;
using CartonStash.Core.ViewModels;

namespace CartonStash.Core.Tests;

/// <summary>Spec 009 amendment R4: the Activity view model's filter, tabs and sort.</summary>
public sealed class ActivityFilterTests
{
    private static (ScriptedInstaller Installer, PackageOperationQueue Queue, FakeSettings Settings, ActivityViewModel Vm) Create(AppSettings? settings = null)
    {
        _ = new TestApp(); // inline synchronization context
        var installer = new ScriptedInstaller();
        var queue = new PackageOperationQueue(installer);
        var fake = new FakeSettings(settings);
        return (installer, queue, fake, new ActivityViewModel(queue, new OperationLogStore(Path.Combine(Path.GetTempPath(), "CartonStashTests", "activity-filter")), new FakeDialogs(), fake));
    }

    private static string[] Names(ActivityViewModel vm) =>
        vm.Entries.Select(e => e is ActivityHeader h ? $"[{h.Name} {h.Count}]" : ((PackageOperation)e).Package.Id).ToArray();

    [Fact]
    public void Sequence_follows_enqueue_and_retry() // R4.5
    {
        var (installer, queue, _, _) = Create();
        var a = queue.Enqueue(OperationKind.Upgrade, Pkg.Installed("A", update: true));
        var b = queue.Enqueue(OperationKind.Upgrade, Pkg.Installed("B", update: true));
        Assert.True(a.Sequence < b.Sequence);

        installer.Complete("A", OperationResult.Failed, "no");
        a.RetryCommand.Execute(null);

        Assert.True(a.Sequence > b.Sequence);
    }

    [Fact]
    public void Entries_follow_the_queue_with_dividers() // R4.4, R4.5
    {
        var (installer, queue, _, vm) = Create();
        queue.Enqueue(OperationKind.Upgrade, Pkg.Installed("A", update: true));
        queue.Enqueue(OperationKind.Upgrade, Pkg.Installed("B", update: true));
        installer.Report("A", OperationPhase.Running);

        Assert.Equal(["[Active 2]", "A", "B"], Names(vm)); // running, then queued in run order

        installer.Complete("A", OperationResult.Failed, "no");

        Assert.Equal(["[Active 1]", "B", "[Failed 1]", "A"], Names(vm));
        Assert.Equal(("All (2)", "Active (1)", "Done", "Failed (1)"), (vm.AllTabText, vm.ActiveTabText, vm.DoneTabText, vm.FailedTabText));
    }

    [Fact]
    public void A_state_change_moves_cards_without_a_reset() // R4.6
    {
        var (installer, queue, _, vm) = Create();
        queue.Enqueue(OperationKind.Upgrade, Pkg.Installed("A", update: true));
        queue.Enqueue(OperationKind.Upgrade, Pkg.Installed("B", update: true));
        var a = vm.Entries[1];
        var b = vm.Entries[2];
        var actions = new List<NotifyCollectionChangedAction>();
        vm.Entries.CollectionChanged += (_, e) => actions.Add(e.Action);

        installer.Complete("A");

        Assert.DoesNotContain(NotifyCollectionChangedAction.Reset, actions);
        Assert.Same(b, vm.Entries[1]); // same objects, new places
        Assert.Same(a, vm.Entries[3]);
    }

    [Fact]
    public void Filter_and_tab_combine_and_clear_filter_resets_both() // R4.1, R4.3, R4.7
    {
        var (installer, queue, _, vm) = Create();
        queue.Enqueue(OperationKind.Upgrade, Pkg.Installed("Git.Git", "Git", update: true));
        installer.Complete("Git.Git");
        queue.Enqueue(OperationKind.Install, Pkg.Installed("Zoom.Zoom", "Zoom"));

        vm.FilterText = "git";
        Assert.Equal(["[Done 1]", "Git.Git"], Names(vm));
        Assert.False(vm.HasNoMatch);

        vm.Tab = ActivityTab.Active;
        Assert.Empty(vm.Entries);
        Assert.True(vm.HasNoMatch);

        vm.ClearFilterCommand.Execute(null);
        Assert.Equal(ActivityTab.All, vm.Tab);
        Assert.Equal("", vm.FilterText);
        Assert.Equal(["[Active 1]", "Zoom.Zoom", "[Done 1]", "Git.Git"], Names(vm));
    }

    [Fact]
    public void Empty_list_is_not_a_no_match() // R4.7
    {
        var (_, _, _, vm) = Create();
        vm.FilterText = "anything";

        Assert.True(vm.IsEmpty);
        Assert.False(vm.HasNoMatch);
    }

    [Fact]
    public void Clear_finished_removes_hidden_finished_operations_too() // R4.9
    {
        var (installer, queue, _, vm) = Create();
        queue.Enqueue(OperationKind.Upgrade, Pkg.Installed("A", update: true));
        installer.Complete("A");
        queue.Enqueue(OperationKind.Upgrade, Pkg.Installed("B", update: true));
        installer.Complete("B", OperationResult.Failed, "no");
        vm.Tab = ActivityTab.Failed;

        vm.ClearFinishedCommand.Execute(null);

        Assert.Empty(queue.Operations);
        Assert.Empty(vm.Entries);
    }

    [Fact]
    public void Sort_is_saved_and_restored() // R4.8
    {
        var (_, _, settings, vm) = Create();

        vm.Sort = ActivitySort.Name;

        Assert.Equal(ActivitySort.Name, settings.Current.ActivitySort);
        Assert.Equal("Sort: Name A to Z", vm.SortText);
        Assert.Equal(ActivitySort.Name, Create(settings.Current).Vm.Sort);
    }

    [Fact]
    public void Live_finishes_and_sort_changes_match_a_fresh_arrangement() // R4.4, R4.6: the 2026-11-12 VM run
    {
        var (installer, queue, _, vm) = Create();
        string[] ids = ["fzf", "jq", "Just", "rg", "zoxide"];
        foreach (var id in ids)
        {
            queue.Enqueue(OperationKind.Upgrade, Pkg.Installed(id, update: true));
        }
        foreach (var id in ids)
        {
            installer.Report(id, OperationPhase.Running);
            installer.Complete(id, id == "jq" ? OperationResult.Failed : OperationResult.Succeeded, "x");
            Assert.Equal(ActivityArranger.Arrange(queue.Operations, "", ActivityTab.All, vm.Sort).Entries, vm.Entries);
        }
        Assert.Equal(["[Failed 1]", "jq", "[Done 4]", "zoxide", "rg", "Just", "fzf"], Names(vm));

        foreach (var sort in new[] { ActivitySort.Name, ActivitySort.Oldest, ActivitySort.Newest, ActivitySort.Default })
        {
            vm.Sort = sort;
            Assert.Equal(ActivityArranger.Arrange(queue.Operations, "", ActivityTab.All, sort).Entries, vm.Entries);
        }
    }

    [Fact]
    public void SyncTo_reaches_the_wanted_order() // R4.6
    {
        var target = new ObservableCollection<object> { "a", "b", "c", "d" };

        ActivityViewModel.SyncTo(target, ["d", "x", "b", "a"]);

        Assert.Equal(["d", "x", "b", "a"], target);
    }
}
