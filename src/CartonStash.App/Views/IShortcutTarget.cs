namespace CartonStash.App.Views;

/// <summary>What the open page does with the window's Ctrl+F and F5 (019 R2, R3).</summary>
public interface IShortcutTarget
{
    /// <summary>Focuses the page's search or filter box and selects its text; false when it has none (R2.1, R2.2).</summary>
    bool FocusSearch();

    /// <summary>Runs the page's Refresh, or nothing while one runs (R3.1, R3.2); false when the page has no Refresh.</summary>
    bool Refresh();
}
