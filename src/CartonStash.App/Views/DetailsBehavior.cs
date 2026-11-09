using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using CartonStash.Core.ViewModels;

namespace CartonStash.App.Views;

/// <summary>
/// View-only glue shared by the list pages (005 R1.1, R4.1): click or Enter on a row opens the details pane,
/// Esc closes it, and focus goes back to the row that opened it.
/// </summary>
internal static class DetailsBehavior
{
    /// <summary>Below this page width the pane covers the list instead of sitting beside it (005 layout).</summary>
    private const double SideBySideMinWidth = 860;

    private const double PaneWidth = 380;

    /// <summary>The page's part: side by side or covering, and Esc closes the pane.</summary>
    public static void Attach(Page page, PackageDetailsViewModel details, FrameworkElement pane)
    {
        // Measured on the page, not the window, so the navigation pane's width is accounted for.
        page.SizeChanged += (_, e) =>
        {
            var sideBySide = e.NewSize.Width >= SideBySideMinWidth;
            Grid.SetColumn(pane, sideBySide ? 1 : 0);
            Grid.SetColumnSpan(pane, sideBySide ? 1 : 2);
            pane.Width = sideBySide ? PaneWidth : double.NaN;
        };

        var escape = new KeyboardAccelerator { Key = VirtualKey.Escape };
        escape.Invoked += (_, e) =>
        {
            if (details.IsOpen)
            {
                details.Close();
                e.Handled = true;
            }
        };
        page.KeyboardAccelerators.Add(escape);
        // Otherwise WinUI shows an "Esc" tooltip when hovering anything on the page.
        page.KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementMode.Hidden;
    }

    /// <summary>A list's part: click or Enter on a row opens the pane, and closing it focuses that row again.
    /// A page can attach several lists (014 R1.7: the Updates page's Excluded list).</summary>
    public static void AttachList(ListView list, PackageDetailsViewModel details)
    {
        PackageRowViewModel? opener = null;

        list.IsItemClickEnabled = true;
        list.ItemClick += (_, e) =>
        {
            if (e.ClickedItem is PackageRowViewModel row)
            {
                opener = row;
                _ = details.LoadAsync(row);
            }
        };

        details.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(PackageDetailsViewModel.Row) && e.PropertyName != nameof(PackageDetailsViewModel.IsOpen))
            {
                return;
            }
            // Another list opened the pane: this one no longer owns the focus return.
            if (opener is not null && details.Row != opener)
            {
                opener = null;
            }
            if (e.PropertyName == nameof(PackageDetailsViewModel.IsOpen) && !details.IsOpen && opener is not null)
            {
                list.ScrollIntoView(opener);
                list.DispatcherQueue.TryEnqueue(() =>
                    (list.ContainerFromItem(opener) as Control)?.Focus(FocusState.Keyboard));
            }
        };
    }
}
