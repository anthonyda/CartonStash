# 019 – Keyboard shortcuts: Design

**Status:** Done
**Requirements:** [requirements.md](requirements.md)

## Overview

All shortcuts are WinUI `KeyboardAccelerator`s on the main window's root grid, so they work wherever the focus is in
that window (R5.2). The window decides what a key means: page keys call the existing `OpenPage`, Ctrl+4 does what the
Activity item does, and Ctrl+F and F5 are passed to the open page through a small interface, because only the page knows
its own box and its own Refresh. Tooltips and `AutomationProperties.AcceleratorKey` are written in XAML by hand, so WinUI's
automatic accelerator tooltips stay off everywhere (R4.3). No WinGet code changes.

## Components

| Layer | Type | Responsibility | New / changed |
|-------|------|----------------|---------------|
| App/Views | `IShortcutTarget` | `bool FocusSearch()` and `bool Refresh()`; each returns false when the page has nothing for the key | New |
| App/Views | `SearchPage` | Implements `FocusSearch` (QueryBox); `Refresh` returns false | Changed |
| App/Views | `InstalledPage` | Implements `FocusSearch` (filter box, named `FilterBox`) and `Refresh` | Changed |
| App/Views | `UpdatesPage` | Implements `Refresh`; `FocusSearch` returns false | Changed |
| App | `MainWindow` | Registers the accelerators, routes them, sets the Settings item's tooltip | Changed |
| App/Services | `DialogService` | New `IsOpen`, true while a ContentDialog is shown | Changed |
| Core/ViewModels | `InstalledViewModel`, `UpdatesViewModel` | None: `IsLoading` and `RefreshCommand` already exist | None |

## WinGet API usage

None.

## Data flow

Key press → `KeyboardAccelerator.Invoked` on `Root` → `MainWindow.Shortcut_Invoked` →

- dialog open (`DialogService.IsOpen`) → not handled, nothing happens (R5.1);
- Ctrl+1/2/3/5 → `OpenPage("search" | "installed" | "updates" | "settings")` (R1.1, R1.2);
- Ctrl+4 → `ToggleActivity()` (R1.3);
- Ctrl+F → focus inside the Activity pane? → `ActivityFilter.Focus` + `SelectAll` (R2.3); else
  `(ContentFrame.Content as IShortcutTarget)?.FocusSearch()` (R2.1, R2.2);
- F5 / Ctrl+R → `(ContentFrame.Content as IShortcutTarget)?.Refresh()` (R3.1).

`args.Handled` is set to the result, so a key the page doesn't use goes on as usual.

`Refresh()` on Installed and Updates: `if (ViewModel.IsLoading) return true;` (R3.2, the key is used but does nothing),
else `ViewModel.RefreshCommand.Execute(null)`.

`FocusSearch()`: `box.Focus(FocusState.Keyboard)` then `box.SelectAll()`.

`ToggleActivity()`: if `ActivitySplit.IsPaneOpen` and `DisplayMode == Overlay`, close it; if closed, open it (same as
`ShowActivity`). A pinned inline pane stays open, and the existing `PaneClosing` handler would refuse anyway.

### Accelerator list

| Keys | Modifiers | Meaning |
|------|-----------|---------|
| `Number1`, `NumberPad1` … `Number5`, `NumberPad5` | Control | R1.1 to R1.3 |
| `F` | Control | R2 |
| `F5` | none | R3 |
| `R` | Control | R3 |

Both number rows count, so Ctrl+1 works with the number pad too. On the AZERTY keyboard of the test VM the top row
gives `Number1` with Ctrl as well (the virtual key is the same; only Shift changes the character), which the manual
check confirms.

## UI

- `Root.KeyboardAcceleratorPlacementMode = Hidden`, as `DetailsBehavior` already does per page, so WinUI adds no
  automatic tooltips (R4.3).
- Tooltips (R4.1, R4.2), written in XAML: navigation items "Search (Ctrl+1)", "Installed (Ctrl+2)", "Updates
  (Ctrl+3)", "Activity (Ctrl+4)". Settings is the built-in item, so `MainWindow` sets "Settings (Ctrl+5)" on
  `Nav.SettingsItem` once `Nav` is loaded (BR-32: the item doesn't exist before). Refresh buttons: "Refresh (F5)" on
  both pages (Installed has none today).
- Narrator (R5.3): `AutomationProperties.AcceleratorKey` on the same elements, plus the search box ("Control+F") and the
  Installed filter box. Values in UIA's form: "Control+1", "Control+F", "F5".
- The Activity filter box gets `x:Name="ActivityFilter"` and `AcceleratorKey="Control+F"`.

## Error handling and edge cases

- **Text boxes (R5.2):** WinUI `TextBox` uses none of these keys, so accelerators fire from inside a box. Ctrl+R is
  right-align only in `RichEditBox`, which the main window doesn't use.
- **Release notes view:** `NotesView` is a read-only control; it doesn't use these keys either. Checked by hand.
- **Dialogs (R5.1):** while a `ContentDialog` is open, focus is inside its popup, and `IsOpen` is the guard if WinUI
  still routes an accelerator to `Root`. Set in `ShowAsync` around `dialog.ShowAsync()`.
- **Other windows:** the WinGet setup window and the finish-update window register nothing; the keys do nothing there.
- **Before navigation is ready:** `OpenPage("settings")` already handles a missing Settings item (BR-32).
- **Activity item selection:** `OpenPage` sets `Nav.SelectedItem` to a page item, never to Activity, so
  `Nav_SelectionChanged` doesn't open the pane by mistake.

## Testing strategy

No view-model logic changes, so no new unit tests (Q3). Everything is checked by hand in the test VM, never on the host:

- R1 to R3 with the keys from the Search box, from a list, from the details pane and from the Activity filter.
- R3.2: hold F5 on Installed; one refresh runs to the end.
- R4: hover each navigation item and Refresh button; no "Esc" tooltip anywhere.
- R5.1: open the uninstall confirmation, press Ctrl+2 and F5; nothing happens behind it.
- R5.3: Narrator reads the shortcut on each navigation item, Refresh button and box.
- Light, dark and high contrast for the new tooltips (Q5).

## Risks and alternatives considered

- **Accelerators on each page instead of the window:** keeps page logic local, but Ctrl+1 to Ctrl+5 must work on every
  page and inside the Activity pane, which sits outside the page. One place is simpler.
- **WinUI's automatic tooltips:** they append the key to the tooltip of the element that owns the accelerator. Our
  accelerators are owned by `Root`, and the automatic text would show on unrelated controls, so it stays off.
- **Ctrl+4 when the pane is pinned:** it does nothing. Unpinning by keyboard stays with the pin button.
