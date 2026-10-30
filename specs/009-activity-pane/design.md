# 009 – Activity pane: Design

**Status:** Approved (2026-10-30). Amendment R2.7 approved 2026-10-30
**Requirements:** [requirements.md](requirements.md)

## Overview

Three changes to one pane. The card redesign (R1) is mostly XAML over a richer `PackageOperation`. Logs (R2) add a small
`OperationLogStore` and pass a log path to WinGet. Pinning (R3) switches the existing `SplitView` between overlay and inline.
No new WinGet concepts: everything goes through the existing queue and installer (constitution A2).

## Components

| Layer | Type | Responsibility | New / changed |
|-------|------|----------------|---------------|
| Core/Models | `PackageOperation` | add `StartedAt`, `FinishedAt`, `LogFilePath`, `Details`; `StateText`, `TimeText`, `State` (enum for the icon) | changed |
| Core/Models | `OperationOutcome` | add `Details` (WinGet status, installer exit code, extended error) for the summary | changed |
| Core/Services | `IOperationLogStore` / `OperationLogStore` | Log folder (`%LOCALAPPDATA%\CartonStash\logs`, or the package's `LocalFolder\logs`), new file path per run, write summary, read log text, prune to 50 | new |
| Core/Services | `IPackageInstaller.RunAsync` | new `logPath` parameter → `InstallOptions.LogOutputPath` / `UninstallOptions.LogOutputPath` | changed |
| Core/Services | `PackageOperationQueue` | Asks the log store for a path before each run, stamps times, writes the summary afterwards; keeps active operations first (R1.5) | changed |
| Core/Models | `AppSettings` | add `ActivityPinned` (bool, default false; older files load as false) | changed |
| Core/ViewModels | `ActivityViewModel` | `IsPinned`, `LogCommand` target, log text loading | changed |
| Core/Services | `IDialogService` | `ShowLogAsync(title, text, folder)` | changed |
| App | `MainWindow` Activity pane | Card template, header Pin toggle, `SplitView` mode switching | changed |
| App | `DialogService` | Log dialog: monospace, selectable, scrollable; Copy, Open folder, Close | changed |

## Card (R1)

```
┌────────────────────────────────────────────────┐
│ [icon]  Git                              14:32 │
│         Update · ✕ Failed                      │
│         The installer failed with code 1603.   │
│                               [Log]  [Retry]   │
└────────────────────────────────────────────────┘
```

- Card: `CardBackgroundFillColorDefaultBrush`, stroke, 8 px corners, 8 px between cards.
- State icon + words: `FontIcon` glyphs per state (clock, ring while running, check, warning, error, cancelled) next to the state text, so
  color is never the only cue. Failed: `SystemFillColorCriticalBrush` icon and a critical border. Restart required: caution.
- Running: the same progress bar as today, under the state line.
- `PackageIcon` (005) is reused. Rows' icons come from `PackageRowViewModel`; here the card builds its own row-less request through
  `PackageActions.GetIconAsync`, so `PackageIcon` gains a `Package` property next to `Row`.
- Ordering (R1.5): the queue inserts new operations at index 0 today, so newest is on top. When an operation finishes while
  others are active, it moves below the active ones.

## Logs (R2)

- Path per run: `logs\<yyyyMMdd-HHmmss>-<kind>-<sanitized id>.log`; Retry gets a new file.
- **The folder must be the real path.** Inside the MSIX, `%LOCALAPPDATA%` is virtualized for the app, while WinGet's server is another
  process. So the packaged app uses `ApplicationData.Current.LocalFolder`, which is a real path. T1 verifies WinGet can write there.
- WinGet passes the log option to the installer where the installer type supports it (MSI, Inno, Nullsoft, Burn…). Portable, zip and
  MSIX packages usually write nothing, hence R2.3. *T1 (2026-10-30):* `LogOutputPath` wrote a 25 KB log for LocalSend (Inno, user scope);
  uninstalling the same package succeeded but wrote no log, so uninstalls will mostly show the summary only.
- After the operation the queue prepends the summary to the file (or creates it), so a log always exists. All log I/O is caught and ignored
  for the operation's result (R2.5); a failed write leaves `LogFilePath` null and the dialog shows the in-memory summary.
- Pruning: at startup, keep the 50 newest `*.log` by time.
- Copy uses `IClipboardService`. Open folder uses `Launcher.LaunchFolderPathAsync`.
- Large logs: the dialog shows the last 1 MB, with a note, and Copy copies the same text. Text goes into a `TextBox` with `IsReadOnly` (plain text, R2.6).
- Names and messages are user-visible text from WinGet and manifests; the summary never includes secrets because the app handles none.

## Pinning (R3)

- `ActivitySplit.DisplayMode`: `Overlay` unpinned, `Inline` pinned; `IsPaneOpen` true while pinned.
- Pin toggle: `ToggleButton` with the pin glyph in the pane header, bound two-way to `ActivityViewModel.IsPinned`, which saves `ActivityPinned` (R3.4).
- `MainWindow.SizeChanged`: below 1000 px (R3.5), use `Overlay` and leave the pane closed until asked; at 1000 px or more, restore `Inline`. The saved value doesn't change.
- `PaneClosing`: cancel it while inline, so only the Pin toggle closes it (R3.2). The close button is hidden while pinned (R3.6).
- Details pane: pages switch side-by-side by their own width (spec 005, 860 px), so pinning narrows the page and the details pane
  covers the list when there's no room. No extra work, but checked in T7.

## Data flow

Enqueue → queue stamps `StartedAt` when the worker takes it, gets a log path → installer runs with it → outcome → queue stamps
`FinishedAt`, writes the summary, sets `LogFilePath` → card shows the Log button. Log button → `ActivityViewModel` reads the text → `ShowLogAsync`.

## Error handling and edge cases

- Log folder not writable: operation unaffected; dialog shows the in-memory summary (R2.5).
- Clear finished removes cards, not log files.
- The WinGet server refuses the log path: seen as an operation failure would be wrong, so T1 checks this; if it happens only on some types, the log path is left out for them.
- A finished operation retried: the card returns to active, with new times and a new log.

## Amendment R2.7: log privacy note (approved 2026-10-30)

- `DialogService.ShowLogAsync` shows one line above the log: "Logs can include your user name, PC name and file paths. Check before sharing."
- It stays visible while scrolling, and Copy still copies the log as it is.

## Testing strategy

| Criteria | How |
|----------|-----|
| R1.3–R1.5, state text, time text, ordering | Unit tests on `PackageOperation` and the queue |
| R2.2, R2.3, R2.5 | `OperationLogStoreTests` (temp folder: summary, installer log appended, prune to 50, unwritable folder) |
| R2.1, R2.4 | `ActivityViewModelTests` with fake dialogs and clipboard |
| Log path passed to WinGet | Unit test on options mapping |
| R3.4, older settings | `SettingsTests` |
| R1.1, R1.2, R1.6, R1.7, R3.1–R3.3, R3.5, R3.6 | Manual (UIA + screenshots, dark and light, keyboard) |
| Real logs | Manual: an MSI or Inno package from the app (portable + MSIX app), and a failing one |

## Risks and alternatives considered

- **`LogOutputPath` in the packaged app:** unverified; T1 first, because it decides the folder.
- **Alternative: the log as a page or a pane inside Activity.** Rejected for v1: a dialog needs no new navigation.
- **Alternative: parse WinGet's own diagnostic logs per operation.** Rejected: files aren't per-operation.
- **Alternative: a real `ListView` grouping (Active / Finished).** Rejected: more chrome; ordering and state icons do the job.
