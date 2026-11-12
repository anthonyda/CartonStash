using CartonStash.Core.Models;

namespace CartonStash.Core.ViewModels;

/// <summary>What the Activity pane shows: entries (a header or an operation) and the tab counts (009 R4.1–R4.5).</summary>
public sealed record ActivityArrangement(IReadOnlyList<object> Entries, int All, int Active, int Done, int Failed)
{
    public int CountOf(ActivityTab tab) => tab switch
    {
        ActivityTab.Active => Active,
        ActivityTab.Done => Done,
        ActivityTab.Failed => Failed,
        _ => All,
    };
}

/// <summary>Every filter, tab and sort rule of the Activity pane, with no UI (009 R4).</summary>
public static class ActivityArranger
{
    // R4.4: the order of the groups in the All tab.
    private static readonly ActivityTab[] GroupOrder = [ActivityTab.Active, ActivityTab.Failed, ActivityTab.Done];

    /// <summary>R4.2: Queued and Running are Active; Failed is Failed; every other end is Done.</summary>
    public static ActivityTab CategoryOf(PackageOperation operation) => operation.State switch
    {
        OperationState.Queued or OperationState.Running => ActivityTab.Active,
        OperationState.Failed => ActivityTab.Failed,
        _ => ActivityTab.Done,
    };

    /// <summary>R4.1: name, ID or action contains the text, ignoring case. Empty text matches everything.</summary>
    public static bool Matches(PackageOperation operation, string? filter)
    {
        var text = filter?.Trim();
        return string.IsNullOrEmpty(text)
            || operation.Package.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
            || operation.Package.Id.Contains(text, StringComparison.OrdinalIgnoreCase)
            || operation.KindText.Contains(text, StringComparison.OrdinalIgnoreCase);
    }

    public static ActivityArrangement Arrange(IEnumerable<PackageOperation> operations, string? filter, ActivityTab tab, ActivitySort sort)
    {
        var matching = operations.Where(o => Matches(o, filter)).ToList();
        var byCategory = GroupOrder.ToDictionary(c => c, c => Sorted(matching.Where(o => CategoryOf(o) == c), sort));

        var entries = new List<object>();
        if (tab == ActivityTab.All)
        {
            foreach (var category in GroupOrder)
            {
                var group = byCategory[category];
                if (group.Count > 0)
                {
                    entries.Add(new ActivityHeader(category, group.Count));
                    entries.AddRange(group);
                }
            }
        }
        else
        {
            entries.AddRange(byCategory[tab]);
        }

        return new ActivityArrangement(entries, matching.Count,
            byCategory[ActivityTab.Active].Count, byCategory[ActivityTab.Done].Count, byCategory[ActivityTab.Failed].Count);
    }

    // R4.5, inside one group.
    private static List<PackageOperation> Sorted(IEnumerable<PackageOperation> operations, ActivitySort sort) => sort switch
    {
        ActivitySort.Newest => operations.OrderByDescending(o => o.StartedAt).ThenByDescending(o => o.Sequence).ToList(),
        ActivitySort.Oldest => operations.OrderBy(o => o.StartedAt).ThenBy(o => o.Sequence).ToList(),
        ActivitySort.Name => operations.OrderBy(o => o.Package.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(o => o.Sequence).ToList(),
        _ => operations
            .OrderBy(o => o.State switch { OperationState.Running => 0, OperationState.Queued => 1, _ => 2 })
            .ThenBy(o => o.IsActive ? o.Sequence : 0)
            .ThenByDescending(o => o.FinishedAt)
            .ThenByDescending(o => o.Sequence)
            .ToList(),
    };
}
