namespace CartonStash.Core.Models;

/// <summary>The Activity pane's tabs (009 R4.2).</summary>
public enum ActivityTab
{
    All,
    Active,
    Done,
    Failed,
}

/// <summary>The Activity pane's sort orders (009 R4.5), saved in Settings (R4.8).</summary>
public enum ActivitySort
{
    /// <summary>The running operation, then queued ones in run order, then finished ones newest first.</summary>
    Default,
    Newest,
    Oldest,
    Name,
}

/// <summary>A divider in the All tab: a category's name and how many cards follow it (009 R4.4).</summary>
public sealed record ActivityHeader(ActivityTab Category, int Count)
{
    public string Name => Category.ToString();

    public bool IsFailed => Category == ActivityTab.Failed;

    public string CountText => Count.ToString(System.Globalization.CultureInfo.CurrentCulture);

    /// <summary>"Failed, 1 operation" for Narrator (R4.10).</summary>
    public string AccessibleName => $"{Name}, {Count} {(Count == 1 ? "operation" : "operations")}";
}
