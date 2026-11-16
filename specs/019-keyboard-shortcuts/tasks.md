# 019 – Keyboard shortcuts: Tasks

**Status:** In progress
**Design:** [design.md](design.md)

Tick a task only when its verification passes. Keep tasks small enough for one commit.

- [x] **T1.** `DialogService.IsOpen`, true around `dialog.ShowAsync()` in `ShowAsync`. *(R5.1)*
  - Verify: build; manual in T7.
- [x] **T2.** `IShortcutTarget` (`FocusSearch`, `Refresh`). `SearchPage`: `FocusSearch` focuses and selects QueryBox.
  `InstalledPage`: `FocusSearch` on the filter box (`x:Name="FilterBox"`), `Refresh` skips while `IsLoading`, else runs
  `RefreshCommand`. `UpdatesPage`: `Refresh` the same way. *(R2.1, R2.2, R3.1, R3.2)*
  - Verify: build; manual in T7.
- [x] **T3.** `MainWindow`: accelerators on `Root` (Ctrl+1 to Ctrl+5 on both number rows, Ctrl+F, F5, Ctrl+R),
  `KeyboardAcceleratorPlacementMode = Hidden`, one `Shortcut_Invoked` handler with the dialog guard; `ToggleActivity`;
  Ctrl+F goes to `ActivityFilter` when the focus is inside the Activity pane. *(R1.1–R1.3, R2.3, R3.1, R4.3, R5.1, R5.2)*
  - Verify: build; manual in T7.
- [x] **T4.** Tooltips: navigation items "Search (Ctrl+1)" to "Activity (Ctrl+4)" in XAML, "Settings (Ctrl+5)" on
  `Nav.SettingsItem` once `Nav` is loaded; "Refresh (F5)" on the Installed and Updates Refresh buttons. *(R4.1, R4.2)*
  - Verify: build; manual in T7.
- [x] **T5.** `AutomationProperties.AcceleratorKey` on the navigation items (Settings in code), both Refresh buttons,
  QueryBox, the Installed filter box and the Activity filter box. *(R5.3)*
  - Verify: build; manual in T7.
- [x] **T6.** `dotnet build` and `dotnet test --filter "Category!=Integration"` pass (Q4).
  - Verify: command output.
- [ ] **T7.** End check in the test VM (AZERTY): *(all)*
  - Ctrl+1 to Ctrl+5 (top row and number pad) from the search box, a list, the details pane and the Activity filter.
  - Ctrl+4 opens and closes the overlay pane; does nothing while pinned.
  - Ctrl+F on Search, Installed, Updates (nothing), Settings (nothing), and inside the Activity pane.
  - F5 and Ctrl+R on Installed and Updates; F5 held on Installed runs one refresh.
  - Uninstall confirmation open: Ctrl+2 and F5 do nothing behind it.
  - Tooltips on the navigation items and Refresh buttons; no "Esc" tooltip; Esc still closes details and empties the
    Activity filter.
  - Narrator reads the shortcuts; dark and high contrast.
  - Verify: manual, recorded below.
  - Fix found in T7: the Installed page's Refresh button had no accessible name (its content is an icon and a text
    block), so Narrator read nothing for it. It now has `AutomationProperties.Name="Refresh"` (R5.3, Q5).
- [ ] **T8.** CHANGELOG entry (next version); roadmap status.

## Manual verification log

| Criterion | Date | Result | Notes |
|-----------|------|--------|-------|
| R1.1–R1.3 | 2026-11-16 | Pass | VM, Store build 019b, `guest	019.ps1` (AZERTY, keys sent as virtual keys): Ctrl+1/2/3/5 from the search box, top row and number pad; Ctrl+4 opens and closes the overlay, leaves a pinned pane open |
| R2.1–R2.3 | 2026-11-16 | Pass | Ctrl+F focuses the search box, the Installed filter (from a row) and the Activity filter (from inside the pane); does nothing on Updates |
| R3.1, R3.2 | 2026-11-16 | Pass | F5 and Ctrl+R show the loading bar on Installed, F5 on Updates; F5 held 3 s: the refresh finishes, no crash |
| R4.1–R4.3 | 2026-11-16 | Pass | Tooltips "Search (Ctrl+1)" … "Settings (Ctrl+5)", "Activity (Ctrl+4)", "Refresh (F5)" on both pages; no Esc tooltip on a row; Esc still closes the details pane |
| R5.1 | 2026-11-16 | Pass | Uninstall confirmation open: Ctrl+1, F5, Ctrl+4 change nothing behind it |
| R5.2 | 2026-11-16 | Pass | Shortcuts work with the focus in the search box, a filter box, a row and the Activity pane |
| R5.3 | 2026-11-16 | Pass (UIA) | `AcceleratorKey` exposed on the navigation items, Refresh and the filter boxes (after the Refresh name fix). Narrator itself not listened to |
| Q5 themes | — | Not checked | Dark and high contrast for the new tooltips (standard ToolTip, no custom colors) |
