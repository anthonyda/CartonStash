# 009 – Activity pane: Design

**Status:** Approved (2026-10-30). Amendment R2.7 approved 2026-10-30. Amendment R2.8–R2.10 approved 2026-10-30. Amendment R4 approved 2026-11-12
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
- WinGet passes the log option to the installer where the installer type supports it (MSI, Inno, Burn…). Nullsoft, portable, zip and
  MSIX packages usually write nothing, hence R2.3. *Corrected 2026-10-30:* Firefox (Nullsoft) ran with only `/S /PreventRebootRequired=true`, and Mozilla documents no log option. *T1 (2026-10-30):* `LogOutputPath` wrote a 25 KB log for LocalSend (Inno, user scope);
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

## Amendment R2.8–R2.10: WinGet's record in the log (approved 2026-10-30)

- **What exists.** WinGet's server writes `WinGetCOM-*.log` to `%LOCALAPPDATA%\Packages\Microsoft.DesktopAppInstaller_8wekyb3d8bbwe\LocalState\DiagOutputDir`.
  It holds the download URL, hash check, installer arguments and the installed entry WinGet matched. Each file has a `Caller:` line
  (`CartonStash.exe` unpackaged, the package family name in the MSIX).
- **Evidence (2026-10-30, dev PC, WinGet 1.29.380).** 13 of 13 operations had their own file, including runs 5 to 10 seconds apart and all on one server process.
  This overturns the earlier reason for keeping these logs out of scope, but it comes from one PC and one WinGet version, so matching stays strict.
- **T12 (2026-10-30).** A process started inside the installed 1.0.1 MSIX (`Invoke-CommandInDesktopPackage`) listed the folder and read a file with shared access. `jq` and `celq`, queued
  together from the unpackaged app, got one file each, created after their `StartedAt`: 18.151 and 20.169 s, with the operations finishing at about 19.78 and 21.61 s. WinGet's last write came about 10 ms before `Complete`.
- **New service.** `IWinGetRecordReader` / `WinGetRecordReader` in `Core/Services`, with the folder passed to the constructor so tests can use a temp folder.
  `Find(startedAt, finishedAt)` returns the text or a reason it has none. It reads a text file and doesn't touch the COM API, so A2 is unaffected.
- **Matching (R2.9).** Take `WinGetCOM-*.log` files created between `StartedAt` and `FinishedAt` whose `Caller:` is this app. The queue runs one operation at a time, so
  there should be exactly one. None or several: no record, and the log says why.
- **Reading.** Open with `FileShare.ReadWrite | FileShare.Delete`, because WinGet may still hold the file. Copy the text into our log, because WinGet cleans up its own files.
- **Cleaning (R2.10).** Line by line, before `Compose`: strip user info, query and fragment from `http(s)` URLs, and blank the value of the `Installer download headers:` line.
- **Log layout (R2.8).** Summary, `--- Installer log ---`, then `--- WinGet log ---`. `OperationLogStore.Complete` gets the cleaned text and `Compose` adds the section.
- **Privacy.** The record holds local paths with the user name, which the R2.7 line already warns about.
- **Failures.** All of this is caught like other log I/O (R2.5).

## Testing strategy

| Criteria | How |
|----------|-----|
| R1.3–R1.5, state text, time text, ordering | Unit tests on `PackageOperation` and the queue |
| R2.2, R2.3, R2.5 | `OperationLogStoreTests` (temp folder: summary, installer log appended, prune to 50, unwritable folder) |
| R2.1, R2.4 | `ActivityViewModelTests` with fake dialogs and clipboard |
| Log path passed to WinGet | Unit test on options mapping |
| R2.8–R2.10 | `WinGetRecordReaderTests` (temp folder: one match, none, several, other caller, outside the time range, locked file, URL and header cleaning); `OperationLogStoreTests` for the layout |
| R3.4, older settings | `SettingsTests` |
| R1.1, R1.2, R1.6, R1.7, R3.1–R3.3, R3.5, R3.6 | Manual (UIA + screenshots, dark and light, keyboard) |
| Real logs | Manual: an MSI or Inno package from the app (portable + MSIX app), and a failing one |

## Risks and alternatives considered

- **`LogOutputPath` in the packaged app:** unverified; T1 first, because it decides the folder.
- **Alternative: the log as a page or a pane inside Activity.** Rejected for v1: a dialog needs no new navigation.
- **Alternative: parse WinGet's own diagnostic logs per operation.** Rejected: files aren't per-operation. *Reopened by amendment R2.8–R2.10.*
- **The folder and file format are WinGet internals (amendment R2.8–R2.10).** They can change without notice, so R2.9 treats a missing or unmatched record as normal. T12 checks the MSIX can read the folder at all.
- **Alternative: a real `ListView` grouping (Active / Finished).** Rejected: more chrome; ordering and state icons do the job.


## Amendment R4: filter, tabs and sort (approved 2026-11-12)

This reopens the alternative rejected above: grouping is back, but only in the All tab and as light dividers, not
`ListView` group headers. With a dozen updates queued, ordering alone no longer does the job.

### Layout

```
Activity                     [⇅] [Clear finished] [pin]
[ Filter by name or ID                             ]
 All (9) │ Active (6) │ Done (2) │ Failed (1)
 Active 6 ─────────────────────────────────────────
 ┌ [icon] Node.js                         12:25 PM ┐
 │        Update · ⟳ Updating…                     │
 │                                       [Cancel]  │
 └─────────────────────────────────────────────────┘
 ...
 Failed 1 ─────────────────────────────────────────
 Done 2 ───────────────────────────────────────────
```

Cards are unchanged (R4.9).

### Components

| Layer | Type | Responsibility | New / changed |
|-------|------|----------------|---------------|
| Core/Models | `ActivityTab` (All, Active, Done, Failed), `ActivitySort` (Default, Newest, Oldest, Name) | enums | new |
| Core/Models | `AppSettings.ActivitySort` | saved order, default `Default`; older files load as `Default` (R4.8); checked in `JsonSettingsStore.Load` like the other enums | changed |
| Core/Models | `PackageOperation.Sequence` | a number the queue gives at `Enqueue` and `Retry`, so Default lists queued operations in run order | changed |
| Core/ViewModels | `ActivityArranger` (static, pure) | `Arrange(operations, filter, tab, sort)` → the entries to show (an `ActivityHeader` or an operation) and the four tab counts. Every R4.1–R4.5 rule lives here | new |
| Core/ViewModels | `ActivityViewModel` | `FilterText`, `Tab`, `Sort` (saved), `Entries` (`ObservableCollection<object>`), tab counts and texts, `HasNoMatch`, `ClearFilterCommand`. Re-arranges on every change | changed |
| App | Activity pane in `MainWindow.xaml` | `TextBox`; `SelectorBar` for the tabs; sort `DropDownButton` with a `RadioMenuFlyoutItem` per order; the `ListView` binds `Entries` with a template selector (divider or `OperationCard`); no-match text with **Clear filter** | changed |

### Rules in `Arrange`

- Match: trimmed text, `OrdinalIgnoreCase` `Contains` on `Package.Name`, `Package.Id` and `KindText`. Empty text matches all.
- Category: Queued and Running → Active; Failed → Failed; Succeeded, RestartRequired, Unconfirmed, Cancelled → Done.
- Default: Running, then Queued by `Sequence` ascending; finished by `FinishedAt` descending. Newest / Oldest: `StartedAt`
  (the time on the card), ties by `Sequence`. Name: `Package.Name`, current culture, ignoring case.
- All tab: Active, Failed, Done blocks, each led by `ActivityHeader(name, count)` when it isn't empty. Other tabs: no headers.
- Counts: per tab, after the text filter, before the tab.

### Keeping cards alive (R4.6)

`Entries` is never replaced. After `Arrange`, the view model brings it to the new list with `RemoveAt` and `Insert` (remove
what's gone, then walk the target and put each item into place). Cards that keep their place keep their containers, progress
and focus; a card that changes place is removed and inserted, so it loses focus.

*Changed during T20 (2026-11-12):* the first version used `ObservableCollection.Move`. In the VM, WinUI's `ListView` then drew
the groups out of order (Done above Failed, while the collection was right) and crashed in `Microsoft.UI.Xaml.dll`
(0xc000027b) when the sort changed. Remove and insert avoid both.

It re-arranges when:

- `Operations` changes (enqueue, retry, clear finished),
- an operation's `State` changes (the view model subscribes to each operation's `PropertyChanged` and unsubscribes when it
  leaves the list). Progress ticks (`Percent`, `Phase` inside one state) don't re-arrange, so the list doesn't jump while the
  user scrolls,
- the filter, tab or sort changes. Every keystroke re-arranges: the list is a few dozen items at most.

Headers are records compared by category and count, so a header whose count changed is replaced.

### Accessibility (R4.10)

- Divider: caption-style `TextBlock` with a line after it, `AutomationProperties.HeadingLevel="Level3"`, accessible name with
  the count in words ("Failed, 1 operation"). Failed uses the critical brush and its own word, so it doesn't rely on color.
- Divider containers aren't focusable (`IsTabStop=false`), so Tab moves from card to card.
- Tabs: `SelectorBarItem` text "Active (6)", like the navigation's "Activity (2)"; Narrator reads the text as is. An empty tab shows its name alone.
- Sort: accessible name and tooltip "Sort: Default", updated with the order; the menu marks the current one.
- Esc in the filter box clears it (a `KeyDown` handler, so no accelerator tooltip; Esc on an empty box goes on to the pane as before).

### Testing

| Criteria | How |
|----------|-----|
| R4.1–R4.5, R4.7 | `ActivityArrangerTests`: match on name, ID and action; each tab; counts follow the filter; dividers only in All and only when not empty; each sort, inside groups; queued in run order under Default; no match |
| R4.6 | `ActivityViewModelTests`: a state change moves the entry (collection events recorded: never `Reset`) |
| R4.8 | `SettingsTests`: sort round trip; older file loads as Default |
| R4.9 | `ActivityViewModelTests`: Clear finished with a filter on removes hidden finished operations too |
| R4.6 focus, R4.10 | Manual in the VM (T20): keyboard, Narrator names, light, dark, high contrast; a focused card keeps focus when its update finishes |

### Risks and alternatives

- `SelectorBar` came with Windows App SDK 1.5; the app is on 2.6. If its keyboard handling or counts fall short, a row of
  `RadioButton`s styled as tabs is the fallback.
- **Alternatives shown on 2026-11-12:** chips (A), a filter flyout (B), grouped collapsible sections (D) and a search box
  with an action menu (E). The maintainer chose C, with dividers added.
